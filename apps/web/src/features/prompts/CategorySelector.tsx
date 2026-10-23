import { useApplicationServices } from '../../components/ServiceContext';
import { useEffect, useState } from 'react';


type Category = { id: number; name: string };

export function CategorySelector({ value, onChange, disabled }: { value: number; onChange: (value: number) => void; disabled: boolean }) {
  const { templates } = useApplicationServices();
  const [categories, setCategories] = useState<Category[]>([]);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setError(false);
    templates.categories(controller.signal)
      .then(items => { if (!controller.signal.aborted) setCategories(items); })
      .catch(() => { if (!controller.signal.aborted) setError(true); });
    return () => controller.abort();
  }, [attempt]);
  const available = categories.some(category => category.id === value);
  return <div className="category-field">
    <label htmlFor="prompt-category">Categoría (opcional)</label>
    <select id="prompt-category" value={value} disabled={disabled || !categories.length} onChange={event => {
      const next = Number(event.target.value);
      if (categories.some(category => category.id === next)) onChange(next);
    }}>
      {!available && <option value={value}>{value === 0 ? 'General' : 'Categoría actual'}</option>}
      {categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
    </select>
    {error && <p className="muted" role="status">No se pudieron cargar las categorías. Se conserva tu elección. <button type="button" className="text-button" disabled={disabled} onClick={() => setAttempt(current => current + 1)}>Reintentar categorías</button></p>}
  </div>;
}
