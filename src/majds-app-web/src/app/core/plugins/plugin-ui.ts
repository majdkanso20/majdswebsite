import { Injectable, VERSION } from '@angular/core';

/** The version of the plugin UI contract this shell speaks: what a plugin's element is given (`hostContext`) and how it is mounted (P5 FR-PLUG-019). */
export const HOST_UI_CONTRACT = 1;

/** What the shell learns about a plugin screen from the enabled plugins' manifest entry. */
export interface PluginElementRoute {
  pluginId: string;
  element: string;
  entryUrl: string;
  stylesUrl: string | null;
  contract: number;
  angular: number | null;
}

export type PluginProblem = 'contract' | 'angular';

/**
 * Whether this shell can show a plugin's UI (P5 FR-PLUG-019): it must have been built for this shell's plugin UI contract, and a bundle that shares the
 * host's Angular must have been built for the same Angular major version. Returns what does not match, or null when it is fine.
 */
export function compatibilityProblem(route: Pick<PluginElementRoute, 'contract' | 'angular'>, angularMajor = Number(VERSION.major)): PluginProblem | null {
  if (route.contract !== HOST_UI_CONTRACT) return 'contract';
  if (route.angular !== null && route.angular !== undefined && route.angular !== angularMajor) return 'angular';
  return null;
}

/** Loads a plugin's compiled bundle (an ES module that defines custom elements) once per address. A separate service so tests can replace it. */
@Injectable({ providedIn: 'root' })
export class PluginBundleLoader {
  private readonly loaded = new Map<string, Promise<void>>();

  load(url: string): Promise<void> {
    let pending = this.loaded.get(url);
    if (!pending) {
      pending = import(/* @vite-ignore */ url).then(() => undefined);
      this.loaded.set(url, pending);
      // A failure must not be remembered, or "Try again" could never work.
      pending.catch(() => this.loaded.delete(url));
    }
    return pending;
  }
}
