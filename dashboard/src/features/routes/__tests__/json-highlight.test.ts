import { afterEach, describe, expect, it, vi } from 'vitest'

import { copyToClipboard } from '../json-highlight'

describe('copyToClipboard', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('uses the async clipboard API when it is available', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })

    await expect(copyToClipboard('payload')).resolves.toBe(true)
    expect(writeText).toHaveBeenCalledWith('payload')
  })

  it('falls back to a textarea when the clipboard API is absent', async () => {
    vi.stubGlobal('navigator', {})
    const execCommand = vi.fn().mockReturnValue(true)
    Object.defineProperty(document, 'execCommand', {
      value: execCommand,
      configurable: true,
    })

    await expect(copyToClipboard('payload')).resolves.toBe(true)
    expect(execCommand).toHaveBeenCalledWith('copy')
  })

  it('reports failure when the clipboard API rejects and the fallback is unavailable', async () => {
    vi.stubGlobal('navigator', {
      clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
    })
    Object.defineProperty(document, 'execCommand', {
      value: undefined,
      configurable: true,
    })

    await expect(copyToClipboard('payload')).resolves.toBe(false)
  })

  it('removes the scratch textarea it creates for the fallback', async () => {
    vi.stubGlobal('navigator', {})
    const before = document.body.childElementCount
    Object.defineProperty(document, 'execCommand', {
      value: vi.fn().mockReturnValue(true),
      configurable: true,
    })

    await copyToClipboard('payload')

    expect(document.body.childElementCount).toBe(before)
    expect(document.querySelector('textarea')).toBeNull()
  })
})
