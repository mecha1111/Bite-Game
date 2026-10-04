const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const fixes = [];
const missing = [];
function visit(dir) {
  for (const item of fs.readdirSync(dir, {withFileTypes:true})) {
    if (['.git','.godot','addons','tools','builds','artifacts'].includes(item.name)) continue;
    const file = path.join(dir,item.name);
    if(item.isDirectory()) {visit(file); continue;}
    if(!/\.(tscn|tres|cs|gd|csv|json|godot|cfg|import)$/.test(item.name)) continue;
    const original = fs.readFileSync(file,'utf8');
    const updated = original.replace(/res:\/\/[^"\r\n,;]+/g, reference => {
      const local = reference.slice(6);
      if(fs.existsSync(path.join(root,local))) return reference;
      const normalized = reference.normalize('NFC');
      if(normalized !== reference && fs.existsSync(path.join(root,normalized.slice(6)))) {
        fixes.push({file:path.relative(root,file),before:reference,after:normalized});
        return normalized;
      }
      if(/\.(png|tres|tscn|wav|ogg|mp3|csv|json|ttf)$/.test(reference)) missing.push({file:path.relative(root,file),reference});
      return reference;
    });
    if(process.argv.includes('--fix') && updated !== original) fs.writeFileSync(file,updated,'utf8');
  }
}
visit(root);
fs.writeFileSync(path.join(__dirname,'windows-resource-audit.json'),JSON.stringify({fixes,missing},null,2));
console.log(JSON.stringify({references:fixes.length,files:[...new Set(fixes.map(f=>f.file))],unresolved:missing},null,2));
