/** @type {import('tailwindcss').Config} */
export default {
  // Absolute paths: the dev server process runs with cwd = repo root, not this directory, and
  // Tailwind resolves relative content globs against cwd rather than this file's location.
  content: [`${import.meta.dirname}/index.html`, `${import.meta.dirname}/src/**/*.{js,ts,jsx,tsx}`],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        honey: {
          50:  '#fffbeb',
          100: '#fef3c7',
          200: '#fde68a',
          300: '#fcd34d',
          400: '#fbbf24',
          500: '#f59e0b',
          600: '#d97706',
          700: '#b45309',
          800: '#92400e',
          900: '#78350f',
          950: '#451a03',
        },
        hive: {
          50:  '#fdf8f0',
          100: '#faefd9',
          200: '#f4ddb2',
          300: '#ecc57f',
          400: '#e3a84a',
          500: '#d98d29',
          600: '#c4751f',
          700: '#a35a1b',
          800: '#84481d',
          900: '#6c3c1a',
        }
      },
      fontFamily: {
        display: ['"Playfair Display"', 'Georgia', 'serif'],
        body:    ['"DM Sans"', 'system-ui', 'sans-serif'],
        mono:    ['"JetBrains Mono"', 'monospace'],
      },
      boxShadow: {
        'honey': '0 4px 14px 0 rgba(217, 119, 6, 0.25)',
        'card':  '0 2px 8px 0 rgba(0,0,0,0.08), 0 1px 2px 0 rgba(0,0,0,0.04)',
      },
      animation: {
        'fade-in':    'fadeIn 0.3s ease-out',
        'slide-up':   'slideUp 0.35s ease-out',
        'spin-slow':  'spin 3s linear infinite',
        'scan-line':  'scanLine 2s ease-in-out infinite',
        'float':      'float 6s ease-in-out infinite',
      },
      keyframes: {
        fadeIn:   { from: { opacity: '0' }, to: { opacity: '1' } },
        slideUp:  { from: { opacity: '0', transform: 'translateY(12px)' }, to: { opacity: '1', transform: 'translateY(0)' } },
        scanLine: { '0%, 100%': { top: '0%' }, '50%': { top: '100%' } },
        float:    { '0%, 100%': { transform: 'translateY(0)' }, '50%': { transform: 'translateY(-12px)' } },
      }
    }
  },
  plugins: []
}
