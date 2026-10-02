import React, { useRef, useEffect } from 'react';
import * as maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';

interface MapProps {
    onLocationSelected?: (lat: number, lng: number) => void;
}

const Map: React.FC<MapProps> = ({ onLocationSelected }) => {
    const mapContainer = useRef<HTMLDivElement>(null);
    const map = useRef<maplibregl.Map | null>(null);

    useEffect(() => {
        if (map.current || !mapContainer.current) return; // initialize map only once
        
        const apiKey = import.meta.env.VITE_MAP_API_KEY || ''; // Needs to be provided in MissingInputs
        
        const isDummyKey = apiKey === '' || apiKey.includes('your_maptiler_api_key_here');

        const mapStyle = isDummyKey ? {
            version: 8 as const,
            sources: {
                'osm-tiles': {
                    type: 'raster' as const,
                    tiles: [
                        'https://a.tile.openstreetmap.org/{z}/{x}/{y}.png',
                        'https://b.tile.openstreetmap.org/{z}/{x}/{y}.png',
                        'https://c.tile.openstreetmap.org/{z}/{x}/{y}.png'
                    ],
                    tileSize: 256,
                    attribution: '&copy; OpenStreetMap contributors'
                }
            },
            layers: [{
                id: 'osm-tiles-layer',
                type: 'raster' as const,
                source: 'osm-tiles',
                minzoom: 0,
                maxzoom: 19
            }]
        } : `https://api.maptiler.com/maps/streets-v2/style.json?key=${apiKey}`;

        map.current = new maplibregl.Map({
            container: mapContainer.current,
            style: mapStyle,
            center: [0, 0],
            zoom: 2
        });

        map.current.addControl(new maplibregl.NavigationControl(), 'top-right');
        
        map.current.on('click', (e) => {
            if (onLocationSelected) {
                onLocationSelected(e.lngLat.lat, e.lngLat.lng);
            }
        });
    }, [onLocationSelected]);

    return (
        <div 
            ref={mapContainer} 
            style={{ width: '100%', height: '100vh', position: 'absolute', top: 0, left: 0 }} 
        />
    );
};

export default Map;
