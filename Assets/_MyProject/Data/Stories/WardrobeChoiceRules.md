# Wardrobe choice rules

`wardrobeChoice` supports per-item `optionRules`. Keep the list in the same order as `clothes`.

Example:

```json
"clothes": [
  "free_outfit",
  "premium_outfit"
],
"optionRules": [
  {
    "premiumCost": 0,
    "purchaseKey": "chapter.wardrobe.free_outfit"
  },
  {
    "premiumCost": 25,
    "requiredVariable": "style",
    "requiredValue": 2,
    "requiredItemId": "intro_badge",
    "hideInRestrictedRegions": false,
    "hiddenRegionCodes": ["RU"],
    "purchaseKey": "chapter.wardrobe.premium_outfit",
    "unavailableMessage": "Variant is locked"
  }
]
```

Fields:

- `premiumCost`: hearts cost. `0` means free.
- `purchaseKey`: stable server key for this exact wardrobe item. The client sends it to `NetworkManager.PurchaseWardrobeItem`.
- `requiredVariable` and `requiredValue`: requires `GameState.GetInt(requiredVariable) >= requiredValue`.
- `requiredItemId`: requires an owned wardrobe item.
- `hideInRestrictedRegions` and `hiddenRegionCodes`: same idea as choice region filtering.
- `unavailableMessage`: toast shown when a visible option is blocked.

Security notes:

- The client never applies paid wardrobe items before the purchase call succeeds.
- Authenticated players go through the server spend path.
- Local spending is only for prototype/offline mode and is blocked by `PrototypeFeatureFlags.LocalPremiumSpendEnabled`.
- For full server authority, the backend should validate `purchaseKey` against its own price/restriction table and reject mismatched costs.

Story pack notes:

- Prefix wardrobe ids and `purchaseKey` values with the story or episode id, for example `pp_1.outfit.ritm_goroda`.
- Do not reuse `zls_*`, `outfit_*`, or `hair_*` ids in a different story pack unless the asset is intentionally global.
- If a story has its own wardrobe screen, bind its `WardrobeHeroSetupPage` through `Story Binding -> Story Ids`; JSON still only contains `openWardrobe` and `wardrobeChoice`.
