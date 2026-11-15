import {defineConfig} from 'vite';
export default defineConfig({build:{rollupOptions:{input:{index:'index.html',remote:'remote.html'},output:{
 entryFileNames:chunk=>chunk.name==='remote'?'remote-assets/[name]-[hash].js':'assets/[name]-[hash].js',
 chunkFileNames:'remote-assets/shared-[name]-[hash].js',
 assetFileNames:asset=>asset.names.some(name=>name.startsWith('remote'))?'remote-assets/[name]-[hash][extname]':'assets/[name]-[hash][extname]'
}}}});
