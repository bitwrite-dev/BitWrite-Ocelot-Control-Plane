/**
 * Minimal JSON syntax highlighter for the effective-config preview.
 *
 * Tokenises into React elements rather than HTML strings, so nothing from the
 * payload is ever interpreted as markup. A highlighting library would be
 * several times the size of this for one read-only view.
 */

type TokenKind = 'key' | 'string' | 'number' | 'boolean' | 'punctuation' | 'plain'

const CLASS_BY_KIND: Record<TokenKind, string> = {
  key: 'text-sky-400',
  string: 'text-emerald-400',
  number: 'text-amber-400',
  boolean: 'text-violet-400',
  punctuation: 'text-muted-foreground',
  plain: 'text-foreground',
}

interface Token {
  kind: TokenKind
  value: string
}

const PATTERN =
  /("(?:\\.|[^"\\])*")(\s*:)?|(\b-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|\b(true|false|null)\b|([{}[\],:])/g

function tokenise(json: string): Token[] {
  const tokens: Token[] = []
  let lastIndex = 0

  for (const match of json.matchAll(PATTERN)) {
    const index = match.index ?? 0
    if (index > lastIndex) {
      tokens.push({ kind: 'plain', value: json.slice(lastIndex, index) })
    }

    const [text, quoted, colon, numeric, keyword, punctuation] = match

    if (quoted !== undefined) {
      // A string followed by a colon is a key, not a value.
      tokens.push({ kind: colon !== undefined ? 'key' : 'string', value: quoted })
      if (colon !== undefined) tokens.push({ kind: 'punctuation', value: colon })
    } else if (numeric !== undefined) {
      tokens.push({ kind: 'number', value: numeric })
    } else if (keyword !== undefined) {
      tokens.push({ kind: 'boolean', value: keyword })
    } else if (punctuation !== undefined) {
      tokens.push({ kind: 'punctuation', value: punctuation })
    }

    lastIndex = index + text.length
  }

  if (lastIndex < json.length) {
    tokens.push({ kind: 'plain', value: json.slice(lastIndex) })
  }

  return tokens
}

/** Pretty-prints when the payload parses, and shows it verbatim when it does not. */
function format(payload: string): string {
  try {
    return JSON.stringify(JSON.parse(payload), null, 2)
  } catch {
    return payload
  }
}

export function JsonHighlight({ payload }: { payload: string }) {
  const text = format(payload)

  return (
    <pre className="overflow-x-auto rounded-md border bg-muted/40 p-4 text-xs leading-relaxed">
      <code>
        {tokenise(text).map((token, index) => (
          <span key={index} className={CLASS_BY_KIND[token.kind]}>
            {token.value}
          </span>
        ))}
      </code>
    </pre>
  )
}

/**
 * Copies text to the clipboard, falling back for browsers that withhold the
 * async clipboard API outside a secure context.
 */
export async function copyToClipboard(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // Fall through to the legacy path below.
  }

  try {
    const area = document.createElement('textarea')
    area.value = text
    area.setAttribute('readonly', '')
    area.style.position = 'fixed'
    area.style.opacity = '0'
    document.body.appendChild(area)
    try {
      area.select()
      return document.execCommand('copy')
    } finally {
      // Removed even when execCommand throws, so a failed copy does not leave a
      // hidden element behind in the document.
      document.body.removeChild(area)
    }
  } catch {
    return false
  }
}
