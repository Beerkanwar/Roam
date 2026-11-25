import React, { useRef, useEffect } from 'react';
import * as maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
// MapLibre 6 looks for its worker file next to its own module, which does not exist once the app is
// bundled. Let Vite bundle the worker (with its shared code) and tell MapLibre where it is.
import mapWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url';

maplibregl.setWorkerUrl(mapWorkerUrl);

interface MapProps {
    onLocationSelected?: (lat: number, lng: number) => void;
}

const Map: React.FC<MapProps> = ({ onLocationSelected }) => {
    const mapContainer = useRef<HTMLDivElement>(null);
    const map = useRef<maplibregl.Map | null>(null);

    useEffect(() => {
        if (map.current || !mapContainer.current) return; // initialize map only once
        
        // Default: OpenFreeMap (free vector tiles, no key or sign-up, no usage limits). The public
        // tile.openstreetmap.org servers must not be used by apps. A MapTiler key, if provided at build
        // time, switches to MapTiler's style instead.
        const apiKey = (import.meta.env.VITE_MAP_API_KEY || '').trim();
        const hasMapTilerKey = apiKey !== '' && !apiKey.includes('your_maptiler_api_key_here');
        const mapStyle = hasMapTilerKey
            ? `https://api.maptiler.com/maps/streets-v2/style.json?key=${encodeURIComponent(apiKey)}`
            : 'https://tiles.openfreemap.org/styles/liberty';

        map.current = new maplibregl.Map({
            container: mapContainer.current,
            style: mapStyle,
            center: [0, 0],
            zoom: 2
        });

        map.current.addControl(new maplibregl.NavigationControl(), 'top-right');
        
        let currentMarker: maplibregl.Marker | null = null;

        map.current.on('click', (e) => {
            if (currentMarker) {
                currentMarker.remove();
            }
            
            currentMarker = new maplibregl.Marker({ color: '#d13438' })
                .setLngLat([e.lngLat.lng, e.lngLat.lat])
                .addTo(map.current!);

            if (onLocationSelected) {
                onLocationSelected(e.lngLat.lat, e.lngLat.lng);
            }
        });

        // MapLibre only tracks window resizes. Follow the container too, so the canvas is redrawn when the
        // map is (re)mounted before layout settles (e.g. returning from a live session or a background tab).
        const resizeObserver = new ResizeObserver(() => map.current?.resize());
        resizeObserver.observe(mapContainer.current);

        return () => {
            resizeObserver.disconnect();
            if (currentMarker) {
                currentMarker.remove();
            }
            if (map.current) {
                map.current.remove();
                map.current = null;
            }
        };
    }, [onLocationSelected]);

    return (
        <div 
            ref={mapContainer} 
            style={{ width: '100%', height: '100vh', position: 'absolute', top: 0, left: 0 }} 
        />
    );
};

export default Map;
