// Explicit config path: Vite's dev server process runs with cwd = repo root (not this frontend/
// directory), and Tailwind's own config auto-discovery resolves purely against process.cwd() rather
// than this file's location — without this, it silently falls back to an empty default config.
export default {
  plugins: {
    tailwindcss: { config: `${import.meta.dirname}/tailwind.config.js` },
    autoprefixer: {},
  },
}
