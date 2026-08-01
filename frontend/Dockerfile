FROM node:22-alpine AS build

WORKDIR /app

COPY package*.json ./
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

COPY . .
RUN npm run build -- --configuration production \
    && mkdir -p /app/site \
    && output="$(find /app/dist -type f -name index.html -print -quit)" \
    && test -n "$output" \
    && cp -R "$(dirname "$output")"/. /app/site/

FROM nginx:1.27-alpine

COPY nginx.conf /etc/nginx/nginx.conf
COPY --from=build /app/site/ /usr/share/nginx/html/

EXPOSE 80
