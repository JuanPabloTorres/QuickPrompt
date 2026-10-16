// Production worker blocks this provider under the reviewed consumer terms.
// DOM adapter is retained for explicitly labelled fixture validation, not a bypass.
globalThis.QuickPromptProvider=globalThis.QuickPromptEditor.create({
  host:'claude.ai',paths:['/new','/'],editors:['div.ProseMirror[contenteditable="true"]'],
  send:['button[aria-label="Send message"]','button[aria-label="Enviar mensaje"]'],
  stop:['button[aria-label="Stop response"]'],login:'a[href="/login"],form[action*="login"]'
});
