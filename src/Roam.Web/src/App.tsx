import { useState, useCallback } from 'react';
import Map from './components/Map';
import './App.css';

interface Place {
  id?: string;
  name: string;
  description: string;
  latitude: number;
  longitude: number;
}

function App() {
  const [searchQuery, setSearchQuery] = useState('');
  const [places, setPlaces] = useState<Place[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedCoords, setSelectedCoords] = useState<{lat: number, lng: number} | null>(null);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;

    setLoading(true);
    try {
      const response = await fetch(`https://localhost:7216/api/v1/places/search?q=${encodeURIComponent(searchQuery)}`);
      if (response.ok) {
        const data = await response.json();
        setPlaces(data);
        setSelectedCoords(null);
      } else {
        console.error("Failed to fetch places");
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleRequestTourById = async (placeId: string) => {
    try {
      const response = await fetch(`https://localhost:7216/api/v1/tour-requests`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          placeId: placeId,
          visibility: 0, // Group
          mode: 0, // Immediate
          description: "I'd like to see this place."
        })
      });

      if (response.ok) {
        alert("Tour requested successfully!");
      } else {
        alert("Failed to request tour.");
      }
    } catch (err) {
      console.error(err);
      alert("Error requesting tour.");
    }
  };

  const handleRequestTourByCoords = async () => {
    if (!selectedCoords) return;
    try {
      const response = await fetch(`https://localhost:7216/api/v1/tour-requests/coordinates`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          latitude: selectedCoords.lat,
          longitude: selectedCoords.lng,
          visibility: 0, // Group
          mode: 0, // Immediate
          description: "I'd like to see this specific location."
        })
      });

      if (response.ok) {
        alert("Tour requested successfully at coordinates!");
        setSelectedCoords(null);
      } else {
        alert("Failed to request tour. (Ensure API supports coordinate-based requests)");
      }
    } catch (err) {
      console.error(err);
      alert("Error requesting tour.");
    }
  };

  const handleLocationSelected = useCallback((lat: number, lng: number) => {
    setSelectedCoords({ lat, lng });
    setPlaces([]);
  }, []);

  return (
    <div style={{ position: 'relative', width: '100%', height: '100vh' }}>
      <Map onLocationSelected={handleLocationSelected} />
      
      <div style={{
        position: 'absolute', top: 20, left: 20, zIndex: 10,
        backgroundColor: 'white', padding: '15px', borderRadius: '8px',
        boxShadow: '0 4px 6px rgba(0,0,0,0.1)', width: '300px', color: 'black'
      }}>
        <h1 style={{ fontSize: '1.2rem', margin: '0 0 10px 0', color: 'black' }}>Roam</h1>
        
        <form onSubmit={handleSearch} style={{ display: 'flex', gap: '5px' }}>
          <input 
            type="text" 
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search for a place..."
            style={{ flex: 1, padding: '8px', border: '1px solid #ccc', borderRadius: '4px' }}
          />
          <button type="submit" disabled={loading} style={{ padding: '8px 12px', cursor: 'pointer', backgroundColor: '#555', color: 'white', border: 'none', borderRadius: '4px' }}>
            {loading ? '...' : 'Search'}
          </button>
        </form>

        {places.length > 0 && (
          <ul style={{ listStyle: 'none', padding: 0, margin: '15px 0 0 0', maxHeight: '300px', overflowY: 'auto' }}>
            {places.map((place, i) => (
              <li key={i} style={{ padding: '10px', borderBottom: '1px solid #eee', color: '#333' }}>
                <strong style={{ display: 'block' }}>{place.name}</strong>
                <small>{place.description}</small>
                {place.id && (
                  <button onClick={() => handleRequestTourById(place.id!)} style={{ marginTop: '5px', padding: '5px 10px', cursor: 'pointer', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px' }}>
                    Request Tour
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}

        {selectedCoords && (
          <div style={{ marginTop: '15px', padding: '10px', backgroundColor: '#f9f9f9', borderRadius: '4px', border: '1px solid #eee' }}>
            <strong>Selected Location</strong>
            <p style={{ margin: '5px 0', fontSize: '0.9em', color: '#555' }}>
              Lat: {selectedCoords.lat.toFixed(4)}<br/>
              Lng: {selectedCoords.lng.toFixed(4)}
            </p>
            <button onClick={handleRequestTourByCoords} style={{ width: '100%', padding: '8px', cursor: 'pointer', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px' }}>
              Request Tour Here
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default App;
