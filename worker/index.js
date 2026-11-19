/**
 * Cloudflare Worker (Free Tier) for JBDS-GymApp (.NET 10 Blazor WASM + PWA).
 *
 * - Serves all static Blazor WASM assets free & unlimited via env.ASSETS.
 * - Proxies /api/* requests to the ASP.NET Core 10 + EF Core + Oracle MySQL backend
 *   (hosted on Oracle Cloud Always Free VM behind a Cloudflare Tunnel), forwarding
 *   the original Host header as X-Forwarded-Host so subdomain/custom-domain tenant
 *   resolution works seamlessly!
 */
export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);

    // Handle API routes by proxying to the ASP.NET Core 10 EF Core API origin
    if (url.pathname.startsWith("/api/")) {
      if (!env.API_ORIGIN) {
        return new Response(
          JSON.stringify({
            error: "API_ORIGIN is not configured on this Cloudflare Worker.",
            hint: "Set API_ORIGIN in wrangler.toml or Worker environment variables to your OCI/Cloudflare Tunnel API URL."
          }),
          {
            status: 503,
            headers: { "content-type": "application/json; charset=utf-8" }
          }
        );
      }

      const upstreamBase = env.API_ORIGIN.replace(/\/+$/, "");
      const upstreamUrl = `${upstreamBase}${url.pathname}${url.search}`;

      const headers = new Headers(request.headers);
      // Preserve original host for subdomain/custom-domain multi-tenant resolution
      headers.set("X-Forwarded-Host", url.host);
      headers.set("X-Forwarded-Proto", url.protocol.replace(":", ""));

      const proxyRequest = new Request(upstreamUrl, {
        method: request.method,
        headers,
        body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
        redirect: "manual"
      });

      return fetch(proxyRequest);
    }

    // Serve Blazor WASM PWA static assets (unlimited & free on Cloudflare Workers)
    return env.ASSETS.fetch(request);
  }
};
