/**
 * Phone cameras hand us 12 MP, 3–8 MB JPEGs, and both consumers of a scanned hive photo choke on
 * that: the Groq vision endpoint rejects anything whose base64 exceeds 4 MB (≈3 MB of image bytes,
 * see GroqHiveNumberOcrClient.MaxBase64Bytes), and Tesseract's WASM pass over a full-resolution
 * frame is slow enough to time out or run a mid-range phone out of memory. Re-encoding to a bounded
 * JPEG fixes both and cuts the mobile upload.
 */

/** Longest edge of the re-encoded image — ample for reading a number painted on a hive. */
const MAX_EDGE = 1600

/** Byte budget, kept clear of the backend's ~3 MB ceiling. */
const MAX_BYTES = 2 * 1024 * 1024

/** Tried in order until one lands under MAX_BYTES. */
const QUALITY_STEPS = [0.85, 0.7, 0.55, 0.4]

async function decode(image: Blob): Promise<ImageBitmap | HTMLImageElement> {
  // `imageOrientation` applies the EXIF rotation phone cameras write — without it a portrait shot
  // lands sideways on the canvas and the painted number becomes unreadable to both OCR passes.
  if (typeof createImageBitmap === 'function')
    return createImageBitmap(image, { imageOrientation: 'from-image' })

  // Pre-Safari-15 fallback. Browsers honour EXIF for <img> since ~2020, so orientation still holds.
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

const sourceSize = (source: ImageBitmap | HTMLImageElement) =>
  'naturalWidth' in source
    ? { width: source.naturalWidth, height: source.naturalHeight }
    : { width: source.width, height: source.height }

const toJpeg = (canvas: HTMLCanvasElement, quality: number) =>
  new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', quality))

/**
 * Re-encodes `image` as a JPEG bounded to {@link MAX_EDGE} px on its longest side and
 * {@link MAX_BYTES}, preserving EXIF orientation. Falls back to the original blob when the browser
 * cannot decode or re-encode it, so a failure here degrades to today's behaviour rather than
 * blocking the scan.
 */
export async function downscaleForScan(image: Blob): Promise<Blob> {
  let source: ImageBitmap | HTMLImageElement
  try {
    source = await decode(image)
  } catch {
    return image
  }

  try {
    const { width, height } = sourceSize(source)
    if (!width || !height) return image

    const scale = Math.min(1, MAX_EDGE / Math.max(width, height))
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
      if (encoded.size <= MAX_BYTES) return encoded
    }

    // Still over budget at the lowest quality — send the smallest render we managed rather than the
    // multi-MB original, which the backend would reject outright.
    return encoded ?? image
  } finally {
    if ('close' in source) source.close()
  }
}
