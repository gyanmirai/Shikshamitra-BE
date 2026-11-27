// Run after GenerateDeduplicatedMigrationModels: node scripts/tests/verify-migration-model-deduplication.mjs [generated-directory]
// This verification script is not a build dependency; the build itself requires only .NET.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const sourceDirectory = 'src/ShikshaMitra.EntityFrameworkCore/Migrations';
const generatedDirectory = process.argv[2] ?? 'src/ShikshaMitra.EntityFrameworkCore/obj/Release/net9.0/DeduplicatedMigrations';
const digest = (value) => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
const entityBlock = /^            modelBuilder\.Entity\([\s\S]*?^                \}\);[ \t]*\r?$/gm;
const scopes = new Map();
let callCount = 0;

for (const file of fs.readdirSync(sourceDirectory).filter((name) => name.endsWith('.Designer.cs'))) {
  const original = fs.readFileSync(path.join(sourceDirectory, file), 'utf8')
    .replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
  const namespaceName = original.match(/^namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?:\{|;)/m)?.[1];
  if (!namespaceName) throw new Error(`Missing namespace: ${file}`);
  const imports = [...original.matchAll(/^using [^;\r\n]+;\r?$/gm)].map((match) => match[0].trim());
  const scope = digest(`${namespaceName}\n${imports.join('\n')}`);
  const fragments = scopes.get(scope) ?? new Map();
  scopes.set(scope, fragments);
  const expected = original.replace(entityBlock, (block) => {
    const blockHash = digest(block);
    if (fragments.has(blockHash) && fragments.get(blockHash) !== block) throw new Error('Fragment hash collision');
    fragments.set(blockHash, block);
    callCount += 1;
    return `            global::${namespaceName}.GeneratedMigrationModels_${scope}.Apply_${blockHash}(modelBuilder);`;
  });
  if (expected !== fs.readFileSync(path.join(generatedDirectory, file), 'utf8')) {
    throw new Error(`Generated designer differs from its source: ${file}`);
  }
}

for (const [scope, fragments] of scopes) {
  const helper = fs.readFileSync(path.join(generatedDirectory, `GeneratedMigrationModels_${scope}.g.cs`), 'utf8')
    .replace(/\r\n/g, '\n');
  for (const [blockHash, block] of fragments) {
    if (!helper.includes(`internal static void Apply_${blockHash}(ModelBuilder modelBuilder) {\n${block}\n}\n`)) {
      throw new Error(`Generated fragment differs from its source: ${blockHash}`);
    }
  }
}
console.log(`Verified ${callCount} calls and ${[...scopes.values()].reduce((count, group) => count + group.size, 0)} unchanged fragments across ${scopes.size} scopes.`);
