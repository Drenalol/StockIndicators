import type { MouseEvent } from 'react';
import { API_BASE_URL } from '@/services/api';

/**
 * Absolute URL of one of our own pages, as an outside browser can reach it:
 * the current origin, or the API deployment's origin when the app is served
 * from somewhere a browser could not open (a relative API base means the app
 * and the API share an origin, which is the only thing left to offer).
 */
export function siteUrl(path: string): string {
  return `${siteOrigin()}${path.startsWith('/') ? path : `/${path}`}`;
}

function siteOrigin(): string {
  const current = window.location.origin;

  if (window.location.protocol.startsWith('http') && window.location.hostname !== 'localhost') return current;

  try {
    return new URL(API_BASE_URL).origin;
  } catch {
    return current;
  }
}

/** Opens an absolute URL in a separate tab, leaving the app's own page under it. */
export function openExternalUrl(url: string): void {
  window.open(url, '_blank', 'noopener,noreferrer');
}

/**
 * Click handler for anchors that must leave the page: lets the browser open the
 * new tab (the anchor's own target="_blank") without the SPA router intercepting
 * the navigation.
 */
export function onExternalLinkClick(e: MouseEvent<HTMLAnchorElement>): void {
  if (e.currentTarget.target === '_blank') {
    e.preventDefault();
    openExternalUrl(e.currentTarget.href);
  }
}
