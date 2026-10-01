import React, { useRef, useEffect } from 'react';
import maplibregl from 'maplibre-gl';
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

        map.current = new maplibregl.Map({
            container: mapContainer.current,
            style: `https://api.maptiler.com/maps/streets-v2/style.json?key=${apiKey}`, // Defaulting to MapTiler format
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
