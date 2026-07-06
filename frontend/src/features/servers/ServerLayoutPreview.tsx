type StatItem = {
  label: string
  value: string
}

type ServerLayoutPreviewProps = {
  name: string
  subtitle: string
  stats: StatItem[]
  badgeLabel?: string
}

export function ServerLayoutPreview({
  name,
  subtitle,
  stats,
  badgeLabel = 'Structure ready',
}: ServerLayoutPreviewProps) {
  return (
    <article className="server-preview">
      <div className="server-preview__header">
        <div>
          <h3 className="server-preview__name">{name}</h3>
          <p className="server-preview__subtitle">{subtitle}</p>
        </div>
        <span className="status-pill">{badgeLabel}</span>
      </div>

      <dl className="server-preview__stats">
        {stats.map((stat) => (
          <div key={stat.label} className="server-preview__stat">
            <dt>{stat.label}</dt>
            <dd>{stat.value}</dd>
          </div>
        ))}
      </dl>
    </article>
  )
}
