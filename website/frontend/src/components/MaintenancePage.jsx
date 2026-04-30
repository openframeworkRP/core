import './MaintenancePage.css'

export default function MaintenancePage() {
  return (
    <div className="maintenance-container">
      <div className="maintenance-card">
        <div className="maintenance-icon">🔧</div>
        <h1 className="maintenance-title">Site en maintenance</h1>
        <p className="maintenance-subtitle">
          Nous effectuons des travaux pour améliorer votre expérience.
        </p>
        <p className="maintenance-text">
          Le site sera de retour très prochainement. Merci de votre patience !
        </p>
        <div className="maintenance-footer">
          <span>Small Box Studio</span>
        </div>
      </div>
    </div>
  )
}
