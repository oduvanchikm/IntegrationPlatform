import {useState} from 'react';
import {api} from '../services/api';
import {INTERFACE_TYPES} from '../services/constants';

export function SearchInterfaces({onSelectPublication}) {
    const [filters, setFilters] = useState({
        productName: '',
        interfaceName: '',
        interfaceType: ''
    });
    const [results, setResults] = useState([]);
    const [loading, setLoading] = useState(false);

    const searchInterfaces = async () => {
        setLoading(true);
        try {
            const data = await api.search.advanced(filters);
            setResults(data);
        } catch (error) {
            console.error('Search failed:', error);
            window.alert('Search failed');
        } finally {
            setLoading(false);
        }
    };

    const handleSearch = (e) => {
        e.preventDefault();
        searchInterfaces();
    };

    return (
        <div style={{padding: '20px', border: '1px solid #ccc', margin: '10px'}}>
            <h2>Search Available Interfaces</h2>

            <form onSubmit={handleSearch} style={{marginBottom: '20px'}}>
                <div style={{marginBottom: '10px'}}>
                    <input
                        type="text"
                        placeholder="Product name"
                        value={filters.productName}
                        onChange={(e) => setFilters({...filters, productName: e.target.value})}
                        style={{marginRight: '10px', padding: '5px'}}
                    />
                    <input
                        type="text"
                        placeholder="Interface name"
                        value={filters.interfaceName}
                        onChange={(e) => setFilters({...filters, interfaceName: e.target.value})}
                        style={{marginRight: '10px', padding: '5px'}}
                    />
                    <select
                        value={filters.interfaceType}
                        onChange={(e) => setFilters({...filters, interfaceType: e.target.value})}
                        style={{padding: '5px'}}
                    >
                        <option value="">All types</option>
                        <option value="0">Database</option>
                        <option value="1">API</option>
                        <option value="2">Kafka</option>
                    </select>
                </div>
                <button type="submit" disabled={loading}>
                    {loading ? 'Searching...' : 'Search'}
                </button>
            </form>

            <div>
                <h3>Search Results ({results.length})</h3>
                {results.map(interfaceItem => (
                    <div
                        key={interfaceItem.id}
                        style={{
                            border: '1px solid #ddd',
                            padding: '10px',
                            margin: '5px 0',
                            cursor: 'pointer',
                            backgroundColor: '#f9f9f9'
                        }}
                        onClick={() => onSelectPublication(interfaceItem)}
                        onKeyPress={(e) => e.key === 'Enter' && onSelectPublication(interfaceItem)}
                        tabIndex={0}
                        role="button"
                    >
                        <strong>{interfaceItem.productName} - {interfaceItem.name}</strong>
                        <br/>
                        Type: {INTERFACE_TYPES[interfaceItem.interfaceType]}
                        <br/>
                        {interfaceItem.description}
                        <br/>
                        <small>ID: {interfaceItem.id} | Status: {interfaceItem.connectionStatus}</small>
                    </div>
                ))}
            </div>
        </div>
    );
}