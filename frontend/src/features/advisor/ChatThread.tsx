import { useEffect, useRef } from 'react'
import { Bot } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import type { AdvisorMessage } from '../../core/models'

interface ChatThreadProps {
  messages: AdvisorMessage[]
  pendingUserText?: string | null
  isThinking?: boolean
}

/** Scrollable message list with a "Savjetnik piše…" typing indicator; auto-scrolls to the bottom. */
export function ChatThread({ messages, pendingUserText, isThinking }: ChatThreadProps) {
  const bottomRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages.length, pendingUserText, isThinking])

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-4">
      {messages.map(m => <Bubble key={m.id} role={m.role} content={m.content} />)}
      {pendingUserText && <Bubble role="User" content={pendingUserText} />}
      {isThinking && (
        <div className="flex items-center gap-2 text-sm text-gray-400 dark:text-slate-500">
          <Bot className="w-4 h-4 text-honey-500" />
          <span>Savjetnik piše</span>
          <span className="flex gap-1">
            <span className="w-1.5 h-1.5 rounded-full bg-honey-400 animate-bounce" style={{ animationDelay: '0ms' }} />
            <span className="w-1.5 h-1.5 rounded-full bg-honey-400 animate-bounce" style={{ animationDelay: '150ms' }} />
            <span className="w-1.5 h-1.5 rounded-full bg-honey-400 animate-bounce" style={{ animationDelay: '300ms' }} />
          </span>
        </div>
      )}
      <div ref={bottomRef} />
    </div>
  )
}

function Bubble({ role, content }: { role: AdvisorMessage['role']; content: string }) {
  const isUser = role === 'User'
  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}>
      <div className={`max-w-[85%] rounded-2xl px-4 py-2.5 text-sm leading-relaxed ${
        isUser
          ? 'bg-honey-500 text-white rounded-br-md whitespace-pre-line'
          : 'bg-gray-100 dark:bg-slate-800 text-gray-800 dark:text-slate-100 rounded-bl-md'
      }`}>
        {isUser ? content : <MarkdownMessage content={content} />}
      </div>
    </div>
  )
}

/** Compact markdown for advisor replies — headings/lists/bold render properly instead of raw `**`. */
function MarkdownMessage({ content }: { content: string }) {
  return (
    <ReactMarkdown
      components={{
        p:  props => <p className="mb-2 last:mb-0 leading-relaxed" {...props} />,
        ul: props => <ul className="list-disc pl-4 mb-2 last:mb-0 space-y-1" {...props} />,
        ol: props => <ol className="list-decimal pl-4 mb-2 last:mb-0 space-y-1" {...props} />,
        li: props => <li className="leading-relaxed" {...props} />,
        strong: props => <strong className="font-semibold" {...props} />,
        em: props => <em className="italic" {...props} />,
        h1: props => <h3 className="font-semibold text-[15px] mt-2 first:mt-0 mb-1" {...props} />,
        h2: props => <h3 className="font-semibold text-[15px] mt-2 first:mt-0 mb-1" {...props} />,
        h3: props => <h3 className="font-semibold text-[15px] mt-2 first:mt-0 mb-1" {...props} />,
        a:  props => <a className="underline text-honey-700 dark:text-honey-300 hover:opacity-80" target="_blank" rel="noreferrer" {...props} />,
        code: props => <code className="bg-black/10 dark:bg-white/10 rounded px-1 py-0.5 text-[13px]" {...props} />,
        blockquote: props => <blockquote className="border-l-2 border-honey-400/60 pl-3 italic opacity-90 mb-2" {...props} />,
      }}
    >
      {content}
    </ReactMarkdown>
  )
}
