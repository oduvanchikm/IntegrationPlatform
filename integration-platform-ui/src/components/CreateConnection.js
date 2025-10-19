import {useState} from 'react';
import {api} from '../services/api';
import {INTEGRATION_PATTERNS, INTERFACE_TYPES} from '../services/constants';

const MY_INTERFACES = [
    {id: 1, name: "My Database", interfaceType: 0, description: "Local PostgreSQL"},
    {id: 2, name: "My API", interfaceType: 1, description: "REST API endpoint"},
    {id: 3, name: "My Kafka", interfaceType: 2, description: "Analytics topic"}
];

export function CreateConnection({publicationInterface, onConnectionCreated}) {
    const [myInterfaces] = useState(MY_INTERFACES);
    const [selectedSubscription, setSelectedSubscription] = useState(null);
    const [loading, setLoading] = useState(false);

    const determinePattern = (pubType, subType) => {
        const typeMap = {'0': 'Database', '1': 'Api', '2': 'Kafka'};
        return `${typeMap[pubType]}To${typeMap[subType]}`;
    };

    const handleCreateConnection = async () => {
        if (!selectedSubscription) {
            window.alert('Please select your target interface');
            return;
        }

        setLoading(true);
        try {
            const connectionRequest = {
                publicationInterfaceId: publicationInterface.id,
                subscriptionInterfaceId: selectedSubscription.id,
                integrationPattern: determinePattern(
                    publicationInterface.interfaceType,
                    selectedSubscription.interfaceType
                ),
                scheduleCron: "*/5 * * * *",
                maxRetryAttempts: 3,
                retryDelaySeconds: 60,
                executionTimeoutSeconds: 300
            };

            const result = await api.subscription.connect(connectionRequest);

            if (result.success) {
                window.alert(`Connection created! Config ID: ${result.orchestrationConfigId}`);
                onConnectionCreated(result);
            } else {
                window.alert(`Failed: ${result.error}`);
            }
        } catch (error) {
            console.error('Connection failed:', error);
            window.alert('Connection failed');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{padding: '20px', border: '1px solid #ccc', margin: '10px'}}>
            <h2>Create Connection</h2>

            <div style={{marginBottom: '20px'}}>
                <h3>Source (Publication):</h3>
                <strong>{publicationInterface.productName} - {publicationInterface.name}</strong>
                <br/>
                Type: {INTERFACE_TYPES[publicationInterface.interfaceType]}
                <br/>
                ID: {publicationInterface.id}
            </div>

            <div style={{marginBottom: '20px'}}>
                <h3>Select your target interface:</h3>
                {myInterfaces.map(myInterface => (
                    <div
                        key={myInterface.id}
                        style={{
                            border: '1px solid',
                            borderColor: selectedSubscription?.id === myInterface.id ? 'blue' : '#ddd',
                            padding: '10px',
                            margin: '5px 0',
                            cursor: 'pointer',
                            backgroundColor: selectedSubscription?.id === myInterface.id ? '#e6f3ff' : '#f9f9f9'
                        }}
                        onClick={() => setSelectedSubscription(myInterface)}
                        onKeyPress={(e) => e.key === 'Enter' && setSelectedSubscription(myInterface)}
                        tabIndex={0}
                        role="button"
                    >
                        <strong>{myInterface.name}</strong>
                        <br/>
                        Type: {INTERFACE_TYPES[myInterface.interfaceType]}
                        <br/>
                        {myInterface.description}
                        <br/>
                        <small>ID: {myInterface.id}</small>
                    </div>
                ))}
            </div>

            {selectedSubscription && (
                <div style={{marginBottom: '20px', padding: '10px', backgroundColor: '#f0f0f0'}}>
                    <strong>Integration Pattern:</strong> {
                    INTEGRATION_PATTERNS[
                        determinePattern(publicationInterface.interfaceType, selectedSubscription.interfaceType)
                        ]
                }
                </div>
            )}

            <button
                onClick={handleCreateConnection}
                disabled={!selectedSubscription || loading}
            >
                {loading ? 'Creating...' : 'Create Connection'}
            </button>
        </div>
    );
}