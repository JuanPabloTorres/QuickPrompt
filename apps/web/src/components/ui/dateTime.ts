const clock = new Intl.DateTimeFormat('es', {
  hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h12',
});
const calendarClock = new Intl.DateTimeFormat('es', {
  day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h12',
});

function display(value: string | Date, formatter: Intl.DateTimeFormat): string {
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? 'Fecha no disponible' : formatter.format(date);
}

/** User-facing local time, always 1–12 with a day period, including midnight/noon. */
export function formatTime(value: string | Date): string {
  return display(value, clock);
}

/** Calendar date and 12-hour time in the browser's time zone; stored ISO values stay intact. */
export function formatDateTime(value: string | Date): string {
  return display(value, calendarClock);
}
