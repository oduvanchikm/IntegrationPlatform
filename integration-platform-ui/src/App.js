import { useState } from 'react';
import { SearchInterfaces } from './components/SearchInterfaces';
import { CreateConnection } from './components/CreateConnection';
import { ConnectionList } from './components/ConnectionList';

function App() {
  const [currentStep, setCurrentStep] = useState('search');
  const [selectedPublication, setSelectedPublication] = useState(null);

  const handleSelectPublication = (publicationInterface) => {
    setSelectedPublication(publicationInterface);
    setCurrentStep('create');
  };

  const handleConnectionCreated = (result) => {
    setCurrentStep('list');
    setSelectedPublication(null);
  };

  const handleBackToSearch = () => {
    setCurrentStep('search');
    setSelectedPublication(null);
  };

  return (
      <div style={{ padding: '20px', fontFamily: 'Arial, sans-serif' }}>
        <h1>Integration Platform</h1>

        <div style={{ marginBottom: '20px' }}>
          <button
              onClick={() => setCurrentStep('search')}
              style={{ marginRight: '10px' }}
          >
            Search
          </button>
          <button
              onClick={() => setCurrentStep('list')}
          >
            My Connections
          </button>
        </div>

        {currentStep === 'search' && (
            <SearchInterfaces onSelectPublication={handleSelectPublication} />
        )}

        {currentStep === 'create' && selectedPublication && (
            <div>
              <button onClick={handleBackToSearch} style={{ marginBottom: '10px' }}>
                ← Back to Search
              </button>
              <CreateConnection
                  publicationInterface={selectedPublication}
                  onConnectionCreated={handleConnectionCreated}
              />
            </div>
        )}

        {currentStep === 'list' && (
            <ConnectionList onBackToSearch={() => setCurrentStep('search')} />
        )}
      </div>
  );
}

export default App;