window.pushNotifications = {
    getState: async () => {
        const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
        if (!supported) {
            return { isSupported: false, permission: 'unsupported', isSubscribed: false };
        }

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        return {
            isSupported: true,
            permission: Notification.permission,
            isSubscribed: subscription !== null
        };
    },

    getSubscription: async () => {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            return null;
        }

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        return subscription?.toJSON() ?? null;
    },

    subscribe: async (publicKey) => {
        if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
            throw new Error('Web Push stöds inte av den här webbläsaren.');
        }

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            throw new Error('Du gav inte appen tillåtelse att visa notiser.');
        }

        const registration = await navigator.serviceWorker.ready;
        let subscription = await registration.pushManager.getSubscription();
        if (!subscription) {
            subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(publicKey)
            });
        }

        return subscription.toJSON();
    },

    unsubscribe: async () => {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        return subscription ? subscription.unsubscribe() : true;
    }
};

function urlBase64ToUint8Array(value) {
    const padding = '='.repeat((4 - value.length % 4) % 4);
    const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(base64);
    return Uint8Array.from([...raw].map(character => character.charCodeAt(0)));
}
