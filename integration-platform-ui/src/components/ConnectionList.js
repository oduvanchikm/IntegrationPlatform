import {useState, useEffect} from 'react';
import {api} from '../services/api';

export function ConnectionList({onBackToSearch}) {
    const [connections, setConnections] = useState([]);
    const [loading, setLoading] = useState(true);

    const loadConnections = async () => {
        try {
            const data = await api.subscription.getConnections();
            setConnections(data);
        } catch (error) {
            console.error('Failed to load connections:', error);
        } finally {
            setLoading(false);
        }
    };

    const handleDeleteConnection = async (orchestrationConfigId) => {
        if (!window.confirm('Are you sure you want to delete this connection?')) return;

        try {
            const result = await api.subscription.deleteConnection(orchestrationConfigId);
            if (result.success) {
                alert('Connection deleted');
                loadConnections();
            } else {
                alert(`Delete failed: ${result.error}`);
            }
        } catch (error) {
            console.error('Delete failed:', error);
            alert('Delete failed');
        }
    };

    useEffect(() => {
        loadConnections();
    }, []);

    if (loading) return <div>Loading connections...</div>;

    return (
        <div style={{padding: '20px', border: '1px solid #ccc', margin: '10px'}}>
            <button onClick={onBackToSearch} style={{marginBottom: '10px'}}>
                ← Back to Search
            </button>

            <h2>My Connections ({connections.length})</h2>

            {connections.map(connection => (
                <div key={connection.id} style={{border: '1px solid #ddd', padding: '15px', margin: '10px 0'}}>
                    <strong>Connection #{connection.id}</strong>
                    <br/>
                    Pattern: {connection.integrationPattern}
                    <br/>
                    Schedule: {connection.scheduleCron}
                    <br/>
                    Publication ID: {connection.interfacePublicationId} | Subscription
                    ID: {connection.interfaceSubscriptionId}
                    <br/>
                    Created: {new Date(connection.createdAt).toLocaleString()}
                    <br/>
                    <button
                        onClick={() => handleDeleteConnection(connection.id)}
                        style={{marginTop: '10px', backgroundColor: '#ff4444', color: 'white'}}
                    >
                        Delete
                    </button>
                </div>
            ))}

            {connections.length === 0 && (
                <p>No connections yet. Search for interfaces and create your first connection!</p>
            )}
        </div>
    );
}