import { useState } from 'react';
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

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;

    setLoading(true);
    try {
      // In development, this relies on a proxy or absolute URL to the .NET API
      const response = await fetch(`https://localhost:7154/api/v1/places/search?q=${encodeURIComponent(searchQuery)}`);
      if (response.ok) {
        const data = await response.json();
        setPlaces(data);
      } else {
        console.error("Failed to fetch places");
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ position: 'relative', width: '100%', height: '100vh' }}>
      <Map />
      
      <div style={{
        position: 'absolute', top: 20, left: 20, zIndex: 10,
        backgroundColor: 'white', padding: '15px', borderRadius: '8px',
        boxShadow: '0 4px 6px rgba(0,0,0,0.1)', width: '300px'
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
          <button type="submit" disabled={loading} style={{ padding: '8px 12px', cursor: 'pointer' }}>
            {loading ? '...' : 'Search'}
          </button>
        </form>

        {places.length > 0 && (
          <ul style={{ listStyle: 'none', padding: 0, margin: '15px 0 0 0', maxHeight: '300px', overflowY: 'auto' }}>
            {places.map((place, i) => (
              <li key={i} style={{ padding: '10px', borderBottom: '1px solid #eee', color: '#333' }}>
                <strong style={{ display: 'block' }}>{place.name}</strong>
                <small>{place.description}</small>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

export default App;
