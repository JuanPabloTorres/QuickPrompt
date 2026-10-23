type Props = {
  names: readonly string[];
  values: Readonly<Record<string, string>>;
  defaults?: Readonly<Record<string, string>>;
  idPrefix: string;
  onValueChange: (name: string, value: string) => void;
  maxLength?: number;
  placeholder?: (name: string) => string;
  fieldClassName?: string;
  workspace?: boolean;
};

// Controlled fields only. Callers own state/layout; the server owns parsing,
// compilation and authorization. Command-dialog fields are not workspace targets.
export function PromptVariableFields({ names, values, defaults, idPrefix, onValueChange, maxLength, placeholder, fieldClassName, workspace = false }: Props) {
  return <>{names.map((name, index) => <div className={fieldClassName} key={name}>
    <label htmlFor={`${idPrefix}-${index}`}>{name}</label>
    <textarea id={`${idPrefix}-${index}`} rows={2} maxLength={maxLength}
      data-testid={workspace ? 'variable-field' : undefined}
      data-variable-name={workspace ? name : undefined}
      value={values[name] ?? defaults?.[name] ?? ''}
      placeholder={placeholder?.(name)}
      onChange={event => onValueChange(name, event.target.value)} />
  </div>)}</>;
}
