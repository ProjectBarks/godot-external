// Launching a built grid target and waiting for it to say it is ready.
//
// The target tells us about itself through a ready file (Probe.cs /
// Probe.gd write it in _Ready). That gives the harness three things a bare
// process handle cannot:
//   * the pid, without guessing which of several processes to attach to;
//   * the engine's OWN view of its version/template/precision, so a
//     mislabelled cell directory is caught instead of silently poisoning the
//     matrix (harness.runtime_axes);
//   * an in-process node count, an independent second source for check (e).

import { spawn } from 'node:child_process';
import { existsSync, readFileSync, rmSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

export async function launchTarget(cell, { readyFile, timeoutMs = 60000, headless = false, extraArgs = [], stdio = 'ignore' }) {
  if (!existsSync(cell.exePath)) {
    throw new Error(`target executable missing: ${cell.exePath}`);
  }

  mkdirSync(dirname(readyFile), { recursive: true });
  rmSync(readyFile, { force: true });

  const args = [...extraArgs];
  if (headless) args.push('--headless');

  const child = spawn(cell.exePath, args, {
    cwd: cell.dir,
    stdio,
    env: {
      ...process.env,
      GRID_READY_FILE: readyFile,
      // Never leave a window behind if the harness dies mid-cell.
      GRID_EXIT_AFTER: String(Math.ceil(timeoutMs / 1000) + 120),
    },
  });

  let spawnError = null;
  child.on('error', (err) => { spawnError = err; });

  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (spawnError) throw spawnError;
    if (child.exitCode !== null && !existsSync(readyFile)) {
      throw new Error(`target exited with code ${child.exitCode} before writing a ready file`);
    }
    if (existsSync(readyFile)) {
      // The file may be observed mid-write; retry parse briefly.
      for (let i = 0; i < 20; i++) {
        try {
          const ready = JSON.parse(readFileSync(readyFile, 'utf8'));
          if (ready && ready.pid) return { child, ready };
        } catch { /* still being written */ }
        await delay(50);
      }
    }
    await delay(100);
  }

  await killTarget(child);
  throw new Error(`target did not become ready within ${timeoutMs}ms (no ${readyFile})`);
}

export async function killTarget(child) {
  if (!child || child.exitCode !== null) return;
  try { child.kill(); } catch { /* already gone */ }
  await delay(200);
  if (child.exitCode === null && child.pid && process.platform === 'win32') {
    // Godot spawns a console wrapper on some template configurations; /T gets
    // the whole tree rather than orphaning the real window.
    try {
      spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
    } catch { /* best effort */ }
  }
}

export function readyFilePathFor(harnessRoot, cell) {
  return join(harnessRoot, 'results', '.ready', `${cell.name}.json`);
}
