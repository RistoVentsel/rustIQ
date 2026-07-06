type StateNoticeProps = {
  title: string
  description?: string
  detail?: string
  tone?: 'info' | 'success' | 'warning' | 'error'
}

export function StateNotice({
  title,
  description,
  detail,
  tone = 'info',
}: StateNoticeProps) {
  const className =
    tone === 'error'
      ? 'probe-status probe-status--error'
      : tone === 'success'
        ? 'probe-status probe-status--success'
        : tone === 'warning'
          ? 'probe-status probe-status--warning'
          : 'probe-status probe-status--info'

  return (
    <div className={className}>
      <p className="probe-status__title">{title}</p>
      {description ? <p className="page-text">{description}</p> : null}
      {detail ? <p className="probe-status__detail">{detail}</p> : null}
    </div>
  )
}
