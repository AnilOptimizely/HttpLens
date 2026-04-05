import * as esbuild from 'esbuild';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const watch = process.argv.includes('--watch');

const outDir = path.join(__dirname, '..', 'wwwroot', 'js');
const cssOutDir = path.join(__dirname, '..', 'wwwroot', 'css');

fs.mkdirSync(outDir, { recursive: true });
fs.mkdirSync(cssOutDir, { recursive: true });

// Copy CSS
fs.copyFileSync(
  path.join(__dirname, 'styles', 'httplens.css'),
  path.join(cssOutDir, 'httplens.css')
);

const buildOptions = {
  entryPoints: [path.join(__dirname, 'src', 'index.ts')],
  bundle: true,
  format: 'iife',
  outfile: path.join(outDir, 'httplens.bundle.js'),
  sourcemap: true,
  minify: !watch,
  logLevel: 'info',
};

if (watch) {
  const ctx = await esbuild.context(buildOptions);
  await ctx.watch();
  console.log('Watching for changes...');
} else {
  await esbuild.build(buildOptions);
}
