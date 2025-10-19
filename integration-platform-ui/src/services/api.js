const API_BASE = 'http://localhost:5000/api';

export const api = {
    search: {
        byProduct: (productName) =>
            fetch(`${API_BASE}/search/interfaces/by-product?productName=${encodeURIComponent(productName || '')}`)
                .then(r => r.json()),

        byType: (interfaceType) =>
            fetch(`${API_BASE}/search/interfaces/by-type?interfaceType=${interfaceType || ''}`)
                .then(r => r.json()),

        advanced: (filters) => {
            const params = new URLSearchParams();
            if (filters.productName) params.append('productName', filters.productName);
            if (filters.interfaceName) params.append('interfaceName', filters.interfaceName);
            if (filters.interfaceType) params.append('interfaceType', filters.interfaceType);

            return fetch(`${API_BASE}/search/interfaces/advanced?${params}`)
                .then(r => r.json());
        }
    },

    // Subscription endpoints
    subscription: {
        connect: (connectionRequest) =>
            fetch(`${API_BASE}/subscription/connect`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(connectionRequest)
            }).then(r => r.json()),

        getConnections: () =>
            fetch(`${API_BASE}/subscription/connections`)
                .then(r => r.json()),

        deleteConnection: (orchestrationConfigId) =>
            fetch(`${API_BASE}/subscription/connections/${orchestrationConfigId}`, {
                method: 'DELETE'
            }).then(r => r.json())
    }
};

export const INTERFACE_TYPES = {
    0: 'Database',
    1: 'Api',
    2: 'Kafka'
};

export const INTEGRATION_PATTERNS = {
    'DatabaseToDatabase': 'Database → Database',
    'DatabaseToApi': 'Database → API',
    'DatabaseToKafka': 'Database → Kafka',
    'ApiToDatabase': 'API → Database',
    'ApiToApi': 'API → API',
    'ApiToKafka': 'API → Kafka',
    'KafkaToDatabase': 'Kafka → Database',
    'KafkaToApi': 'Kafka → API',
    'KafkaToKafka': 'Kafka → Kafka'
};