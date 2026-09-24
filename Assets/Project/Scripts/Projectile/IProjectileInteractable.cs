namespace DreamForgeTD.PhysicsPuzzle
{
    public interface IProjectileInteractable
    {
        void OnProjectileHit(Projectile projectile, ProjectileHitContext context);
    }
}
