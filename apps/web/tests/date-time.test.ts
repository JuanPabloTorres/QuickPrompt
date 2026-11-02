import { describe, expect, it } from 'vitest';
import { formatDateTime, formatTime } from '../src/components/ui/dateTime';

describe('visible 12-hour times', () => {
  it.each([
    [0, '12', 'a'], [7, '07', 'a'], [12, '12', 'p'], [13, '01', 'p'], [23, '11', 'p'],
  ])('shows local hour %i with the correct day period', (hour, visibleHour, period) => {
    const date = new Date(2026, 0, 5, hour, 5, 9);
    expect(formatTime(date)).toMatch(new RegExp(`^${visibleHour}:05:09 ${period}\\.\\s*m\\.$`));
    expect(formatDateTime(date)).toMatch(new RegExp(`${visibleHour}:05 ${period}\\.\\s*m\\.$`));
  });

  it('formats an ISO instant locally without modifying the source value', () => {
    const date = new Date(2026, 6, 15, 18, 42, 3);
    const source = date.toISOString();
    expect(formatTime(source)).toMatch(/^06:42:03 p\.\s*m\.$/);
    expect(source).toBe(date.toISOString());
  });

  it('keeps a malformed timestamp from breaking its containing screen', () => {
    expect(formatTime('invalid')).toBe('Fecha no disponible');
    expect(formatDateTime('invalid')).toBe('Fecha no disponible');
  });
});
