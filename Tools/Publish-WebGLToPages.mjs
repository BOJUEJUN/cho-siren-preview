#!/usr/bin/env node
// WebGL 发布暂存入口（跨平台版，替代 Publish-WebGLToPages.ps1）。
// 始终复用 Stage-WebGL.mjs 这一份实现；不加 --apply 时为 dry-run，只打印计划不落盘。
// 用法: node Tools/Publish-WebGLToPages.mjs [--build <path>] [--pages <path>] [--fallback-build <path>] [--dry-run]

import { spawnSync } from 'node:child_process';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const toolDir = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? resolve(args[index + 1]) : fallback;
};

const buildPath = option('--build', join(toolDir, '..', 'Builds', 'WebGL'));
const pagesPath = option('--pages', resolve(toolDir, '..', '..', 'cho-siren-preview'));
const fallbackBuild = option('--fallback-build');
const dryRun = args.includes('--dry-run');

const staged = ['--build', buildPath, '--pages', pagesPath];
if (fallbackBuild) staged.push('--fallback-build', fallbackBuild);
if (!dryRun) staged.push('--apply');

const result = spawnSync(process.execPath, [join(toolDir, 'Stage-WebGL.mjs'), ...staged], { stdio: 'inherit' });
if (result.error) {
  console.error(`[Publish-WebGLToPages] 无法启动 Stage-WebGL.mjs: ${result.error.message}`);
  process.exit(1);
}
if (result.status !== 0) {
  console.error('[Publish-WebGLToPages] WebGL 暂存失败；未执行任何 commit 或 push。');
  process.exit(result.status ?? 1);
}
