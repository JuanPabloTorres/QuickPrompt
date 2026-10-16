globalThis.QuickPromptProvider=globalThis.QuickPromptEditor.create({
  host:'chatgpt.com',paths:['/'],
  editors:['#prompt-textarea[contenteditable="true"]','textarea#prompt-textarea'],
  send:['button[data-testid="send-button"]','button[aria-label="Send prompt"]','button[aria-label="Enviar prompt"]'],
  stop:['button[data-testid="stop-button"]'],
  login:'a[href*="/auth/login"],button[data-testid="login-button"]'
});
