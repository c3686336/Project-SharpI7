# Top-down boss prefab

This feature does not modify a shared scene. Use `Prefabs/TopDownBoss.prefab` when the integration owner is ready to place the boss.

## Integration

1. Make sure the player GameObject uses the `Player` tag, or call `SetPlayerTarget` after spawning the boss.
2. The player's health component should implement `SharpI7.Combat.IDamageable`. As a temporary compatibility path, a public `TakeDamage(float)` method is also detected through `SendMessage`.
3. A spell projectile can damage the boss by calling `BossHealth.TakeDamage(spellDamage)`.
4. Add artwork, animation and collision as children of `TopDownBoss.prefab`; the root prefab owns combat logic only.

The boss cycles through these pattern groups:

- Single ground attack: snapshots the player's position, warns for 2.5 seconds, then explodes.
- Tracking barrage: samples the player's latest position for every strike and creates 3–5 consecutive zones. Each zone warns for 0.8 seconds before exploding.
- Line ground attack: chooses horizontal, vertical, cross or diagonal-cross at random. Long rectangular warnings originate from the boss, warn for 1.5 seconds, and then explode.
- Safe zone attack: covers a 30×18 area in danger, places a circular safe zone 5–9 units away from the player, and damages the player after 3 seconds unless they reach the circle.
- Rotating laser: aims a long beam at the player's position, warns for 1 second, then always rotates 350 degrees clockwise over 5 seconds. It deals damage at most once every 0.5 seconds while touching the player.
- Slow wobble orb: snapshots the player's direction when spawned, moves slowly along that fixed direction while swaying sideways, damages the player on contact, and despawns after completely leaving the configured play-area bounds.
- Radial wobble orbs: fires the same slow projectile in every direction at once. Aimed and radial orb attacks share one family and cannot be selected consecutively; another attack family must execute before either orb pattern can run again.
- Boss distance attack: alternates between an inner-danger "move away" circle and an outer-danger "come close" field centered on the boss. The current player-to-boss distance is checked after a 3-second warning.

Every pattern damages the player only if they are still inside its area. Cross intersections deal damage once, not twice. Death immediately cancels the loop and any active warning.
