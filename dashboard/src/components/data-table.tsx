import type { ReactNode } from 'react'

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/page-state'

/**
 * Server-side paginated table.
 *
 * Pagination, filtering and search are all handled by the API — this component
 * deliberately does no client-side sorting or filtering, so it stays correct as
 * the dataset grows. Every list page needs the same four states, which is why
 * they live here rather than in each page.
 */

export interface Column<T> {
  key: string
  header: string
  cell: (row: T) => ReactNode
  /** Applied to both the header and body cells. */
  className?: string
}

export interface DataTableProps<T> {
  /** Describes the table for screen readers, since the visual layout is ambiguous. */
  caption: string
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  pageSizeOptions?: number[]
  onPageSizeChange?: (size: number) => void
  isLoading?: boolean
  /** Rendered when there are no rows; receives a reset action for filtered views. */
  emptyState?: ReactNode
}

function SkeletonRows({ columns, pageSize }: { columns: Column<unknown>[]; pageSize: number }) {
  return (
    <>
      {Array.from({ length: Math.min(pageSize, 5) }).map((_, rowIndex) => (
        <TableRow key={rowIndex}>
          {columns.map((column) => (
            <TableCell key={column.key} className={column.className}>
              <Skeleton className="h-4 w-full" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  )
}

/** Page numbers with ellipses, e.g. 1 … 4 5 6 … 12 */
function pageWindow(current: number, total: number): (number | 'gap')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)

  const pages: (number | 'gap')[] = [1]
  const start = Math.max(2, current - 1)
  const end = Math.min(total - 1, current + 1)

  if (start > 2) pages.push('gap')
  for (let p = start; p <= end; p += 1) pages.push(p)
  if (end < total - 1) pages.push('gap')
  pages.push(total)

  return pages
}

export function DataTable<T>({
  caption,
  columns,
  rows,
  rowKey,
  totalCount,
  page,
  pageSize,
  onPageChange,
  pageSizeOptions,
  onPageSizeChange,
  isLoading,
  emptyState,
}: DataTableProps<T>) {
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const firstRow = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const lastRow = Math.min(page * pageSize, totalCount)

  return (
    <div className="space-y-4">
      <div className="overflow-x-auto rounded-lg border">
        <Table>
          {/* A real caption, visually hidden: it names the table for screen
              readers without adding a second visible header row. */}
          <caption className="sr-only">{caption}</caption>
          <TableHeader>
            <TableRow>
              {columns.map((column) => (
                <TableHead key={column.key} className={column.className}>
                  {column.header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <SkeletonRows columns={columns as Column<unknown>[]} pageSize={pageSize} />
            ) : rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-40">
                  {emptyState ?? (
                    <EmptyState title="Nothing to show" description="No records match the current filters." />
                  )}
                </TableCell>
              </TableRow>
            ) : (
              rows.map((row) => (
                <TableRow key={rowKey(row)}>
                  {columns.map((column) => (
                    <TableCell key={column.key} className={column.className}>
                      {column.cell(row)}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
        {/* Announced to screen readers; the visual table is not a reliable summary. */}
        <span className="sr-only" role="status" aria-live="polite">
          {isLoading ? 'Loading' : `${totalCount} records`}
        </span>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
        <p className="text-muted-foreground" aria-live="polite">
          {totalCount === 0
            ? 'No records'
            : `Showing ${firstRow}–${lastRow} of ${totalCount}`}
        </p>

        <div className="flex items-center gap-2">
          {onPageSizeChange && pageSizeOptions ? (
            <label className="flex items-center gap-2 text-muted-foreground">
              <span className="sr-only sm:not-sr-only">Rows</span>
              <select
                className="h-8 rounded-md border bg-background px-2 text-sm"
                value={pageSize}
                onChange={(event) => onPageSizeChange(Number(event.target.value))}
                aria-label="Rows per page"
              >
                {pageSizeOptions.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </label>
          ) : null}

          <nav className="flex items-center gap-1" aria-label="Pagination">
            <Button
              variant="outline"
              size="sm"
              onClick={() => onPageChange(page - 1)}
              disabled={page <= 1 || isLoading}
            >
              Previous
            </Button>

            {pageWindow(page, pageCount).map((entry, index) =>
              entry === 'gap' ? (
                <span key={`gap-${index}`} className="px-1 text-muted-foreground">
                  …
                </span>
              ) : (
                <Button
                  key={entry}
                  variant={entry === page ? 'default' : 'ghost'}
                  size="sm"
                  onClick={() => onPageChange(entry)}
                  aria-current={entry === page ? 'page' : undefined}
                  aria-label={`Page ${entry}`}
                >
                  {entry}
                </Button>
              ),
            )}

            <Button
              variant="outline"
              size="sm"
              onClick={() => onPageChange(page + 1)}
              disabled={page >= pageCount || isLoading}
            >
              Next
            </Button>
          </nav>
        </div>
      </div>
    </div>
  )
}
