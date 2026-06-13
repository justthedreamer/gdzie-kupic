# Project Specification

This document is the project specification of the Gdzie Kupic platform.

## Project Vision

### Problem

Finding a specific product locally is frustrating. A buyer looking for a niche item â€” say, a Shure microphone available for pickup in KrakÃ³w today â€” has to search across Allegro, OLX, Google Shopping, and individual store websites one by one. This takes hours and often ends without a result, even if the product exists nearby.

For merchants, the other side of the problem is equally real: small and medium stores have inventory that is never discoverable online, yet maintaining active listings on multiple platforms is costly and time-consuming.

### Solution

Gdzie Kupic is a **reverse marketplace**: instead of searching for products, buyers publish a purchasing request and merchants come to them.

A buyer describes what they need â€” product, location, radius, budget â€” and the platform instantly notifies matching merchants in the area. Merchants respond directly with offers. The buyer receives answers without searching.

This model provides unique value to both sides:
- **Buyers** collapse hours of search into a single action and get real-time feedback on whether their need can be fulfilled locally.
- **Merchants** receive qualified, local demand signals with zero listing effort and zero platform fees.

### Core Experience

The defining feature of the platform is **request transparency**. When a buyer publishes a request, they see a live status panel showing exactly what is happening:

> *"20 merchants were notified. 3 are checking availability. 2 confirmed they have it. 5 said they can't help."*

This eliminates the anxiety of silence and makes the platform feel alive from the first second. The buyer always knows whether to wait or look elsewhere.

### Merchant Response States

When a merchant receives a request notification, they can respond with one of four states:

- **"I have it"** â€” opens a chat thread with the buyer
- **"I may have it"** â€” opens a chat thread with the buyer; merchant can confirm or change state later
- **"I can order it"** â€” opens a chat thread with the buyer; signals the item is not in stock but can be sourced
- **"I can't help"** â€” archived for the merchant; can be reversed while the post is open

### Platform Strategy

The platform is built as a **Progressive Web App (PWA)**, installable on any device with no app store friction. This is intentional: for merchants, the barrier to join must be as close to zero as possible â€” install, register, start receiving local demand.

### Business Value

The long-term vision is a platform where local commerce is demand-driven rather than supply-driven. Future opportunities include integrations with existing marketplaces (Allegro, OLX) and retailer APIs to automatically pull matching offers â€” making the platform an intelligent aggregation layer over the fragmented Polish e-commerce landscape.

---

## Roadmap

### MVP (v1.0) â€” Thesis scope

The MVP validates the core loop: a buyer publishes a request, merchants are notified, merchants respond, the buyer receives offers.

**Buyer**
- Register and log in
- Publish a purchasing request (title, description, product name, location, radius, category, tag, urgency)
- View real-time status of their request (notified / checking / offered / declined counts) â€” visible to post owner only
- Receive push notifications when a merchant responds (fallback to email if opted in)
- Receive and read merchant offers via chat
- Mark a request as fulfilled (found it) or closed (cancelled)
- Reset password via email
- Opt in to email notifications in account settings (disabled by default)

**Merchant**
- Register a shop account (free)
- Set up location and category/tag subscriptions
- Browse a feed of active requests matching their area and subscriptions
- Receive notifications when a new matching request is posted or a new chat message arrives
- Respond to a request: "I have it" / "I may have it" / "I can order it" / "I can't help"
- Change or undo their response at any time while the post is active
- Communicate with the buyer via 1:1 chat (text + images)
- Reset password via email

**Admin**
- Pre-seed and manage the category and tag taxonomy (create, rename, disable)- Ban buyer or merchant accounts; ban takes immediate effect — active posts are expired, chats are locked, platform access is revoked- Accounts are provisioned directly â€” no self-registration

**Platform**
- Location-based request routing (radius filtering with configurable tolerance buffer)
- Category and tag-based merchant matching
- Real-time status panel via SignalR
- Asynchronous notification dispatch
- JWT authentication with refresh token rotation
- PWA â€” installable, push notification support on desktop and mobile

**Out of scope for MVP**
- Guest / anonymous access
- AI-assisted request parsing
- External platform integrations (Allegro, OLX APIs)
- Browser extension
- Merchant analytics dashboard
- Payment processing
- Content moderation

### Go-to-Market Strategy (v1.0)

Launch is buyers-first: acquire buyers through organic channels, then personally onboard merchants in one city (KrakÃ³w) via direct email outreach. When a merchant receives a targeted, human email explaining that buyers in their area are already posting requests, conversion is a simple install â€” no listings, no fees, no commitment.

---

### Future Vision (post-MVP)

**v1.1 â€” Quality & trust**
- Merchant ratings and reviews after fulfilled requests
- Buyer request history and reposting
- Content moderation for chat images

**v1.2 â€” Reach**
- Multi-city expansion
- Merchant category and inventory profile pages
- Advanced radius and category filtering for merchants

**v2.0 â€” Intelligence**
- Integration with retailer APIs and major marketplace platforms to auto-generate offers
- AI agents that search on behalf of the buyer and inject offers into the request feed
- Buyer demand analytics for merchants (trending local needs)