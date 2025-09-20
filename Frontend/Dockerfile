FROM nginx:alpine

COPY *.html /usr/share/nginx/html/

COPY nginx.conf /etc/nginx/nginx.conf

RUN mkdir -p /usr/share/nginx/html/assets

EXPOSE 80