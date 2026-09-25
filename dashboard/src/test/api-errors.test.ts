import { describe, expect, it } from 'vitest'

import { ApiError, normalizeError, normalizeNetworkError } from '@/api/errors'

describe('normalizeError', () => {
  it('flattens the application error shape used by GlobalExceptionMiddleware', () => {
    const result = normalizeError(
      500,
      {
        correlationId: '0HNORATJLQLLV:00000001',
        error: 'Invalid period: 1m. Valid: Second, Minute, Hour, Day',
        type: 'DomainException',
      },
      'fallback',
    )

    expect(result.message).toBe('Invalid period: 1m. Valid: Second, Minute, Hour, Day')
    expect(result.type).toBe('DomainException')
    expect(result.correlationId).toBe('0HNORATJLQLLV:00000001')
    expect(result.isNetworkError).toBe(false)
  })

  it('flattens the ValidationProblemDetails shape emitted by [ApiController]', () => {
    const result = normalizeError(
      400,
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { gatewayId: ['The gatewayId field is required.'] },
        traceId: '00-020f5176cb99b6fdc359b345cb5b3680-6c754d5cf0d4ad2f-00',
      },
      'fallback',
    )

    expect(result.fieldErrors).toEqual({ gatewayId: ['The gatewayId field is required.'] })
    expect(result.message).toBe('gatewayId: The gatewayId field is required.')
    // traceId stands in for correlationId on this shape.
    expect(result.correlationId).toBe('00-020f5176cb99b6fdc359b345cb5b3680-6c754d5cf0d4ad2f-00')
  })

  it('summarises multiple field errors into one readable sentence', () => {
    const result = normalizeError(400, { errors: { key: ['required'], port: ['out of range'] } }, 'x')

    expect(result.message).toBe('key: required; port: out of range')
  })

  it('falls back when the body is not JSON, e.g. an HTML proxy page', () => {
    const result = normalizeError(502, '<html>Bad Gateway</html>', 'GET /api/v1/routes failed with 502')

    expect(result.message).toBe('GET /api/v1/routes failed with 502')
    expect(result.fieldErrors).toBeUndefined()
  })

  it('falls back when the body is empty', () => {
    const result = normalizeError(204, undefined, 'fallback used')

    expect(result.message).toBe('fallback used')
  })

  it('prefers error over title when both are present', () => {
    const result = normalizeError(409, { error: 'specific', title: 'generic' }, 'x')

    expect(result.message).toBe('specific')
  })
})

describe('normalizeNetworkError', () => {
  it('marks a request that never reached the API', () => {
    const result = normalizeNetworkError(new TypeError('Failed to fetch'))

    expect(result.isNetworkError).toBe(true)
    expect(result.status).toBe(0)
    expect(result.message).toContain('Could not reach the API')
  })

  it('handles a non-Error rejection', () => {
    expect(normalizeNetworkError('boom').message).toBe('Could not reach the API: Network request failed')
  })
})

describe('ApiError', () => {
  it('exposes status helpers', () => {
    const notFound = new ApiError({ status: 404, message: 'nope', isNetworkError: false })
    expect(notFound.isNotFound).toBe(true)
    expect(notFound.isUnauthorized).toBe(false)
    expect(notFound.isValidation).toBe(false)

    const unauthorized = new ApiError({ status: 401, message: 'nope', isNetworkError: false })
    expect(unauthorized.isUnauthorized).toBe(true)
  })

  it('is a real Error so it survives being thrown and caught', () => {
    const error = new ApiError({ status: 500, message: 'boom', isNetworkError: false })

    expect(error).toBeInstanceOf(Error)
    expect(() => {
      throw error
    }).toThrow('boom')
  })
})
