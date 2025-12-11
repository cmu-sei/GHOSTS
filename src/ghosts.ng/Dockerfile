# Build stage
FROM node:22-alpine AS builder

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# Production stage
FROM nginx:alpine

RUN rm -rf /usr/share/nginx/html/*
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /app/dist/ghosts.ng/browser /usr/share/nginx/html
COPY docker-entrypoint.sh /docker-entrypoint.d/90-config.sh
RUN sed -i 's/\r$//' /docker-entrypoint.d/90-config.sh && \
    chmod +x /docker-entrypoint.d/90-config.sh

EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
