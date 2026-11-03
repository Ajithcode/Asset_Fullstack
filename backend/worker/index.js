import { Container, getContainer } from "@cloudflare/containers";

export class AssetContainer extends Container {
  defaultPort = 5296; // ApiGateway's existing port, unchanged
  sleepAfter = "30m";

  constructor(ctx, env) {
    super(ctx, env);
    // Set with: npx wrangler secret put MONGODB_URI   (locally: .dev.vars)
    this.envVars = { MONGODB_URI: env.MONGODB_URI };
  }
}

export default {
  async fetch(request, env) {
    const container = getContainer(env.ASSET_CONTAINER);
    return container.fetch(request);
  },
};
