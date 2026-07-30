/**
 * Phone cameras hand us 12 MP, 3–8 MB JPEGs, and both consumers of a scanned hive photo choke on
 * that: the Groq vision endpoint rejects anything whose base64 exceeds 4 MB (≈3 MB of image bytes,
 * see GroqHiveNumberOcrClient.MaxBase64Bytes), and Tesseract's WASM pass over a full-resolution
 * frame is slow enough to time out or run a mid-range phone out of memory. Re-encoding to a bounded
 * JPEG fixes both and cuts the mobile upload.
 */

/** Longest edge of a scanned frame — ample for reading a number painted on a hive. */
const SCAN_MAX_EDGE = 1600

/** Byte budget for a scan, kept clear of the backend's ~3 MB ceiling. */
const SCAN_MAX_BYTES = 2 * 1024 * 1024

/**
 * An inspection photo is looked at by a human and by the frame-analysis model, so it keeps more
 * detail than a scan does. Bounds stay under the 8 MB the upload endpoint accepts.
 */
const PHOTO_MAX_EDGE = 2400
const PHOTO_MAX_BYTES = 6 * 1024 * 1024

/** Tried in order until one lands under the byte budget. */
const QUALITY_STEPS = [0.85, 0.7, 0.55, 0.4]

/**
 * What the upload endpoint stores as-is. The server re-derives this from the header bytes, so this
 * list has to match its sniffer (InspectionPhotoService.DetectContentType).
 */
const DIRECTLY_UPLOADABLE = ['image/jpeg', 'image/png', 'image/webp']

/** Browsers honour EXIF orientation for <img> since ~2020, so this path keeps rotation correct. */
async function decodeViaImageElement(image: Blob): Promise<HTMLImageElement> {
  const url = URL.createObjectURL(image)
  try {
    const el = new Image()
    await new Promise<void>((resolve, reject) => {
      el.onload = () => resolve()
      el.onerror = () => reject(new Error('Image could not be decoded'))
      el.src = url
    })
    return el
  } finally {
    URL.revokeObjectURL(url)
  }
}

async function decode(image: Blob): Promise<ImageBitmap | HTMLImageElement> {
  // `imageOrientation` applies the EXIF rotation phone cameras write — without it a portrait shot
  // lands sideways on the canvas and the painted number becomes unreadable to both OCR passes.
  //
  // Detecting the *function* is not enough, and that is what broke scanning on iPhone: Safari 15–16
  // ship `createImageBitmap`, but their `ImageBitmapOptions.imageOrientation` still carries the
  // original enum ("none" | "flipY"). "from-image" was added to the spec later, so passing it is an
  // invalid enum value and WebKit rejects the call with a TypeError. The rejection escaped to
  // `downscaleForScan`'s outer catch, which returns the image untouched — so on iOS the resize was
  // skipped entirely and the full 12 MP original went on to Tesseract and to the vision endpoint,
  // which refuses anything past its base64 cap. Hence "skeniranje ne radi na iPhonu".
  //
  // The <img> fallback below handles both that and HEIC (iOS decodes HEIC natively; the canvas
  // re-encode then hands every consumer a plain JPEG).
  if (typeof createImageBitmap === 'function') {
    try {
      // Must be awaited here: returning the promise would move the rejection out of this try.
      return await createImageBitmap(image, { imageOrientation: 'from-image' })
    } catch {
      // Fall through. Not retried without the option — the default is "none", which drops EXIF
      // rotation and would land portrait photos sideways.
    }
  }

  return decodeViaImageElement(image)
}

const sourceSize = (source: ImageBitmap | HTMLImageElement) =>
  'naturalWidth' in source
    ? { width: source.naturalWidth, height: source.naturalHeight }
    : { width: source.width, height: source.height }

const toJpeg = (canvas: HTMLCanvasElement, quality: number) =>
  new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', quality))

/**
 * Re-encodes `image` as a JPEG bounded to `maxEdge` px on its longest side and `maxBytes`,
 * preserving EXIF orientation. Falls back to the original blob when the browser cannot decode or
 * re-encode it, so a failure here degrades rather than blocking the caller.
 */
async function reencodeAsJpeg(image: Blob, maxEdge: number, maxBytes: number): Promise<Blob> {
  let source: ImageBitmap | HTMLImageElement
  try {
    source = await decode(image)
  } catch {
    return image
  }

  try {
    const { width, height } = sourceSize(source)
    if (!width || !height) return image

    const scale = Math.min(1, maxEdge / Math.max(width, height))
    const canvas = document.createElement('canvas')
    canvas.width = Math.max(1, Math.round(width * scale))
    canvas.height = Math.max(1, Math.round(height * scale))

    const ctx = canvas.getContext('2d')
    if (!ctx) return image

    // JPEG has no alpha channel; without a white matte a transparent source flattens to black.
    ctx.fillStyle = '#fff'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    ctx.drawImage(source, 0, 0, canvas.width, canvas.height)

    let encoded: Blob | null = null
    for (const quality of QUALITY_STEPS) {
      encoded = await toJpeg(canvas, quality)
      if (!encoded) break
      if (encoded.size <= maxBytes) return encoded
    }

    // Still over budget at the lowest quality — send the smallest render we managed rather than the
    // multi-MB original, which the backend would reject outright.
    return encoded ?? image
  } finally {
    if ('close' in source) source.close()
  }
}

/** Bounded JPEG for the hive-number scan (on-device OCR + the vision endpoint). */
export const downscaleForScan = (image: Blob): Promise<Blob> =>
  reencodeAsJpeg(image, SCAN_MAX_EDGE, SCAN_MAX_BYTES)

/**
 * Makes a picked file uploadable as an inspection photo.
 *
 * An iPhone stores camera shots as HEIC. Safari transcodes to JPEG when the photo comes from the
 * Photo Library, but hands over the raw `.heic` when it is picked through Files/iCloud Drive — and
 * neither the client check nor the server's header sniffer accepts HEIC, so the attachment was
 * refused with "nije podržan format". Safari decodes HEIC natively, so a canvas round-trip turns it
 * into an ordinary JPEG.
 *
 * Files that are already in an accepted format are returned **untouched** — re-encoding them would
 * strip the EXIF the app deliberately preserves (see the note under the picker: photos are stored
 * in their original form, including capture location).
 */
export async function normalizePhotoForUpload(file: File): Promise<File> {
  if (DIRECTLY_UPLOADABLE.includes(file.type)) return file

  const converted = await reencodeAsJpeg(file, PHOTO_MAX_EDGE, PHOTO_MAX_BYTES)
  // reencodeAsJpeg hands back the input untouched when it cannot decode it; passing that through
  // unchanged lets the existing validation produce its normal "unsupported format" message.
  if (converted === file) return file

  return new File([converted], file.name.replace(/\.[^.]+$/, '') + '.jpg', {
    type: 'image/jpeg',
    lastModified: file.lastModified,
  })
}
