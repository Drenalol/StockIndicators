/**
 * How an address typed into a provider field behaves outside the local network.
 *
 * Telegram checks every link button URL on its own servers and refuses loopback and
 * single-label names, and a LAN address is unreachable for a phone that is off the
 * network, so a value like this has to be questioned in the settings page instead of in
 * a failed delivery. The rule mirrors `PublicWebAddress.IsReachable` on the backend.
 */
export type PublicUrlState = 'empty' | 'invalid' | 'local' | 'public';

/** Names that resolve only inside a private network (mDNS, LLMNR, home routers). */
const LOCAL_SUFFIXES = ['.local', '.localdomain', '.internal', '.lan', '.home.arpa'];

export function classifyPublicUrl(value: string): PublicUrlState {
  const trimmed = value.trim();
  if (!trimmed) return 'empty';

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return 'invalid';
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') return 'invalid';

  // an IPv6 literal keeps its brackets in `hostname`
  const host = url.hostname.replace(/^\[/, '').replace(/\]$/, '').toLowerCase();
  if (!host) return 'invalid';

  if (host.includes(':')) return isPublicIpv6(host) ? 'public' : 'local';

  const octets = dottedQuad(host);
  if (octets) return isPublicIpv4(octets) ? 'public' : 'local';

  // "nas" or "router" without a suffix, and the home-network suffixes, resolve on the LAN only
  if (!host.includes('.')) return 'local';
  if (host === 'localhost' || host.endsWith('.localhost')) return 'local';

  return LOCAL_SUFFIXES.some((suffix) => host.endsWith(suffix)) ? 'local' : 'public';
}

function dottedQuad(host: string): number[] | null {
  const parts = host.split('.');
  if (parts.length !== 4 || parts.some((part) => !/^\d{1,3}$/.test(part))) return null;

  const octets = parts.map(Number);
  return octets.every((octet) => octet <= 255) ? octets : null;
}

/** Reserved IPv6 ranges, compared by the leading 16 bits: shortening keeps them intact. */
function isPublicIpv6(host: string): boolean {
  const head = Number.parseInt(host.split(':')[0] || '0', 16);
  if (Number.isNaN(head)) return false;

  // :: (unspecified), ::1 (loopback) and ::ffff:a.b.c.d (v4-mapped) all start with "::"
  if (head === 0) return false;
  if (head >= 0xfe80 && head <= 0xfebf) return false;
  if (head >= 0xfec0 && head <= 0xfeff) return false;
  return !(head >= 0xfc00 && head <= 0xfdff);
}

function isPublicIpv4([a, b]: number[]): boolean {
  // 0.x, loopback 127/8, private 10/8 and 172.16/12 and 192.168/16, link-local 169.254/16,
  // reserved 240/4, and the carrier-grade NAT range a VPN mesh hands out
  if (a === 0 || a === 10 || a === 127 || a === 169 || a >= 240) return false;
  if (a === 172 && b >= 16 && b <= 31) return false;
  if (a === 192 && b === 168) return false;
  return !(a === 100 && b >= 64 && b <= 127);
}
