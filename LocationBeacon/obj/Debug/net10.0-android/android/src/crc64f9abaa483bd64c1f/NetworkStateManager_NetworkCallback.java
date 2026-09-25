package crc64f9abaa483bd64c1f;


public class NetworkStateManager_NetworkCallback
	extends android.net.ConnectivityManager.NetworkCallback
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAvailable:(Landroid/net/Network;)V:GetOnAvailable_Landroid_net_Network_Handler\n" +
			"n_onLost:(Landroid/net/Network;)V:GetOnLost_Landroid_net_Network_Handler\n" +
			"n_onCapabilitiesChanged:(Landroid/net/Network;Landroid/net/NetworkCapabilities;)V:GetOnCapabilitiesChanged_Landroid_net_Network_Landroid_net_NetworkCapabilities_Handler\n" +
			"";
		mono.android.Runtime.register ("LocationBeacon.Services.NetworkStateManager+NetworkCallback, LocationBeacon", NetworkStateManager_NetworkCallback.class, __md_methods);
	}

	public NetworkStateManager_NetworkCallback ()
	{
		super ();
		if (getClass () == NetworkStateManager_NetworkCallback.class) {
			mono.android.TypeManager.Activate ("LocationBeacon.Services.NetworkStateManager+NetworkCallback, LocationBeacon", "", this, new java.lang.Object[] {  });
		}
	}

	public NetworkStateManager_NetworkCallback (int p0)
	{
		super (p0);
		if (getClass () == NetworkStateManager_NetworkCallback.class) {
			mono.android.TypeManager.Activate ("LocationBeacon.Services.NetworkStateManager+NetworkCallback, LocationBeacon", "System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
		}
	}

	public void onAvailable (android.net.Network p0)
	{
		n_onAvailable (p0);
	}

	private native void n_onAvailable (android.net.Network p0);

	public void onLost (android.net.Network p0)
	{
		n_onLost (p0);
	}

	private native void n_onLost (android.net.Network p0);

	public void onCapabilitiesChanged (android.net.Network p0, android.net.NetworkCapabilities p1)
	{
		n_onCapabilitiesChanged (p0, p1);
	}

	private native void n_onCapabilitiesChanged (android.net.Network p0, android.net.NetworkCapabilities p1);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
