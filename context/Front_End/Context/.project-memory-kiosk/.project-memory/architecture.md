# Architecture

- `lib/features/setup`: authentication models, repository, secure session store, setup controller, login and kiosk selection UI.
- `lib/features/kiosk`: menu, cart, checkout, payment, tracking, and kiosk presentation state.
- `lib/main.dart`: restores auth session, presents setup when unconfigured, and creates the kiosk controller for the selected kiosk.
- `lib/core/di`: dependency registration.
