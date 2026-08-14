# Imagen de desarrollo del frontend (React + Vite).
# Ejecuta el dev server de Vite con HMR; el proxy de la API se configura por la
# variable de entorno VITE_API_PROXY_TARGET (ver vite.config.ts).
FROM node:22-alpine
WORKDIR /app
COPY package.json package-lock.json ./
# npm ci corre como root; se cede node_modules al usuario node para que Vite
# pueda escribir su caché de optimización (node_modules/.vite) al correr no-root.
RUN npm ci && chown -R node:node /app/node_modules
COPY . .
EXPOSE 5173
# Habilita polling para que el HMR detecte cambios en bind mounts (Docker/Windows).
ENV CHOKIDAR_USEPOLLING=true
# Usuario no-root de la imagen oficial de node.
USER node
CMD ["npm", "run", "dev", "--", "--host"]
