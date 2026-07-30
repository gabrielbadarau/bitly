import { useState, type FormEvent } from 'react'
import './App.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string

interface CreateShortUrlResponse {
  shortUrl: string
}

function App() {
  const [longUrl, setLongUrl] = useState('')
  const [customAlias, setCustomAlias] = useState('')
  const [expirationDate, setExpirationDate] = useState('')
  const [result, setResult] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setResult(null)
    setIsSubmitting(true)

    try {
      const response = await fetch(`${API_BASE_URL}/urls`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          longUrl,
          customAlias: customAlias || undefined,
          expirationDate: expirationDate ? new Date(expirationDate).toISOString() : undefined,
        }),
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Request failed with status ${response.status}`)
      }

      const data = (await response.json()) as CreateShortUrlResponse
      setResult(data.shortUrl)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page">
      <h1>Bitly Clone</h1>

      <form onSubmit={handleSubmit} className="form">
        <label>
          Long URL
          <input
            type="url"
            required
            value={longUrl}
            onChange={(e) => setLongUrl(e.target.value)}
            placeholder="https://example.com/some/long/path"
          />
        </label>

        <label>
          Custom alias (optional)
          <input
            type="text"
            value={customAlias}
            onChange={(e) => setCustomAlias(e.target.value)}
            placeholder="my-link"
          />
        </label>

        <label>
          Expires at (optional)
          <input
            type="datetime-local"
            value={expirationDate}
            onChange={(e) => setExpirationDate(e.target.value)}
          />
        </label>

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Creating…' : 'Shorten'}
        </button>
      </form>

      {result && (
        <div className="result">
          <a href={result} target="_blank" rel="noreferrer">
            {result}
          </a>
          <button type="button" onClick={() => navigator.clipboard.writeText(result)}>
            Copy
          </button>
        </div>
      )}

      {error && <p className="error">{error}</p>}
    </div>
  )
}

export default App
