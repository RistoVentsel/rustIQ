import { appConfig } from '../lib'

export function ConfigSummary() {
  return (
    <dl className="config-summary">
      <div className="config-summary__item">
        <dt>API base URL</dt>
        <dd>{appConfig.apiBaseUrl}</dd>
      </div>
      <div className="config-summary__item">
        <dt>Hub URL</dt>
        <dd>{appConfig.apiHubUrl}</dd>
      </div>
    </dl>
  )
}
