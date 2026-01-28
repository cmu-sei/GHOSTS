FROM node:20.11.1-alpine AS base

# libc6-compat for nextjs. This is needed at runtime so its fine to have in the base container
RUN apk add --no-cache libc6-compat

# Stage for installing deps
FROM base AS deps
WORKDIR /app

# Deps needed to build the Better-sqlite3 package. These only exist in the deps stage so no bloat in the final image
RUN apk add --no-cache python3 make gcc g++

COPY package.json package-lock.json ./
RUN npm ci --legacy-peer-deps

RUN npm install sharp --legacy-peer-deps

# Stage for building the nextjs app
FROM base AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

COPY drizzle.config.ts ./
COPY src ./src

ENV NEXT_TELEMETRY_DISABLED=1
ENV NEXT_DISABLE_ESLINT=true
ENV NEXT_DISABLE_TYPECHECKING=true
RUN npx drizzle-kit push --config=drizzle.config.ts && cp sqlite.db /tmp/sqlite.db
RUN npm run build

# Final stage to run the app
FROM base AS runner

# Add sqlite CLI for inspection/debugging
RUN apk add --no-cache sqlite

RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs

# Make user the owner of the /app directory because SQLite needs to create journal files to function
RUN mkdir app
RUN chown -R nextjs app

WORKDIR /app

ENV NODE_ENV=production

# Set the correct permission for prerender cache
RUN mkdir .next
RUN chown nextjs:nodejs .next

# Automatically leverage output traces to reduce image size
# https://nextjs.org/docs/advanced-features/output-file-tracing
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=builder --chown=nextjs:nodejs /app/public ./public

# Copy migrated database file to runner
COPY --from=builder --chown=nextjs:nodejs /tmp/sqlite.db ./sqlite.db
RUN chmod 777 sqlite.db

USER nextjs

EXPOSE 8080

ENV PORT 8080

# server.js is created by next build from the standalone output
CMD HOSTNAME="0.0.0.0" node server.js
