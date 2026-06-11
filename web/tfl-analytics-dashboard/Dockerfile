FROM node:24-alpine AS build
WORKDIR /app

COPY web/tfl-analytics-dashboard/package*.json ./
RUN npm ci

COPY web/tfl-analytics-dashboard/ ./
RUN npm run build

FROM nginx:1.29-alpine
COPY web/tfl-analytics-dashboard/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/tfl-analytics-dashboard/browser /usr/share/nginx/html

EXPOSE 80
