import { ArrowUpRight, Braces, Star } from 'lucide-react';
import type { Prompt } from '../../services/api';

export function PromptStream({ items, open }: { items: Prompt[]; open: (prompt: Prompt) => void }) {
  return <ol className="prompt-stream" aria-label="Prompts guardados">
    {items.map((prompt, index) => <li key={prompt.id}>
      <button className="prompt-item" aria-label={prompt.title} aria-describedby={`prompt-description-${prompt.id}`} onClick={() => open(prompt)}>
        <span className="stream-number" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
        <span className="prompt-summary"><span className="prompt-title">{prompt.title}{prompt.isFavorite && <Star className="favorite-mark" size={15} aria-label="Favorito" />}</span>
          <span className="prompt-description" id={`prompt-description-${prompt.id}`}>{prompt.description || prompt.templateText}</span></span>
        <span className="variable-count"><Braces size={15} aria-hidden="true" />{prompt.variableNames.length ? `${prompt.variableNames.length} variables` : 'Listo para copiar'}</span>
        <ArrowUpRight className="stream-arrow" size={21} aria-hidden="true" />
      </button>
    </li>)}
  </ol>;
}
