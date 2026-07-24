import ReactMarkdown from 'react-markdown'

/**
 * App-styled markdown renderer for learning articles (SPEC-06). No raw-HTML plugins on purpose:
 * react-markdown's default escaping renders `<script>` and friends as inert text (the XSS guard).
 */
export function MarkdownArticle({ markdown }: { markdown: string }) {
  return (
    <ReactMarkdown
      components={{
        h1: props => <h2 className="font-display text-xl font-bold text-gray-900 dark:text-slate-100 mt-6 mb-3 first:mt-0" {...props} />,
        h2: props => <h2 className="font-display text-lg font-semibold text-gray-900 dark:text-slate-100 mt-6 mb-3 first:mt-0" {...props} />,
        h3: props => <h3 className="font-display text-base font-semibold text-gray-800 dark:text-slate-200 mt-5 mb-2" {...props} />,
        p: props => <p className="text-[15px] leading-relaxed text-gray-700 dark:text-slate-300 mb-4" {...props} />,
        ul: props => <ul className="list-disc pl-5 mb-4 space-y-1.5 text-[15px] text-gray-700 dark:text-slate-300" {...props} />,
        ol: props => <ol className="list-decimal pl-5 mb-4 space-y-1.5 text-[15px] text-gray-700 dark:text-slate-300" {...props} />,
        li: props => <li className="leading-relaxed" {...props} />,
        a: props => <a className="text-honey-600 dark:text-honey-400 underline hover:text-honey-700" target="_blank" rel="noreferrer" {...props} />,
        strong: props => <strong className="font-semibold text-gray-900 dark:text-slate-100" {...props} />,
        blockquote: props => <blockquote className="border-l-4 border-honey-300 dark:border-honey-500/50 pl-4 italic text-gray-600 dark:text-slate-400 mb-4" {...props} />,
        code: props => <code className="bg-gray-100 dark:bg-slate-800 rounded px-1.5 py-0.5 text-sm" {...props} />,
        table: props => <div className="overflow-x-auto mb-4"><table className="w-full text-sm border-collapse" {...props} /></div>,
        th: props => <th className="text-left font-semibold text-gray-800 dark:text-slate-200 border-b border-gray-200 dark:border-slate-700 px-3 py-2" {...props} />,
        td: props => <td className="text-gray-700 dark:text-slate-300 border-b border-gray-100 dark:border-slate-800 px-3 py-2" {...props} />,
      }}
    >
      {markdown}
    </ReactMarkdown>
  )
}
