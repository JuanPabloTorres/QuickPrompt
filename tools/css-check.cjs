// Uses only the local, already authorized CLI. Never invokes npx or downloads.
const fs=require('fs'),path=require('path'),os=require('os'),{spawnSync}=require('child_process');
const repo=path.resolve(__dirname,'..'),cli=path.join(repo,'node_modules/@tailwindcss/cli/dist/index.mjs'),target=path.join(repo,'src/QuickPrompt.Web/wwwroot/quickprompt.css');
if(!fs.existsSync(cli)){console.error('BLOCKED: Tailwind CLI absent. Installation requires user authorization.');process.exit(2);}
const temp=path.join(os.tmpdir(),'quickprompt-css-'+require('crypto').randomUUID()+'.css');
try{const r=spawnSync(process.execPath,[cli,'-i','src/QuickPrompt.Web/Styles/input.css','-o',temp,'--minify'],{cwd:repo,stdio:'inherit',windowsHide:true});if(r.status!==0)process.exitCode=1;else if(!fs.existsSync(target)||!fs.readFileSync(temp).equals(fs.readFileSync(target))){console.error('CSS differs from compiled source; run npm run css:build.');process.exitCode=1;}else console.log('PASS_CSS_REPRODUCIBLE');}finally{if(fs.existsSync(temp))fs.unlinkSync(temp);}
