/** File contract: UTF-8 without BOM, LF lines; preserve all other characters. */
export function downloadPreparedText(text: string): void {
  const bytes = new TextEncoder().encode(text.replace(/\r\n?/g, '\n'));
  const url = URL.createObjectURL(new Blob([bytes], { type: 'text/plain;charset=utf-8' }));
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'quickprompt.txt';
    anchor.click();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
