
using UdonSharp;
using VRC.SDKBase;

namespace nmSeashore
{
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class PickupEventReceiver : UdonSharpBehaviour
	{
		public WearableBuoyancy target;

		public override void OnPickup() => target.enterGuide();
		public override void OnDrop() => target.endGuide();
		public override void OnPickupUseDown() => target.enterBuoy();

		public override void OnOwnershipTransferred(VRCPlayerApi player)
		{
			if(Networking.IsOwner(player, target.gameObject) == true) { return; }

			Networking.SetOwner(player, target.gameObject);
		}
	}
}