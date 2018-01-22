import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AppModuleShared } from './app.shared.module';
import { AppComponent } from './components/app/app.component';
import { EventSocketService } from './services/eventsocket/eventsocket.service';

@NgModule({
    bootstrap: [AppComponent],
    imports: [
        BrowserModule,
        AppModuleShared
    ],
    providers: [
        { provide: 'BASE_URL', useFactory: getBaseUrl },
        { provide: 'WEBSOCKET_URL', useFactory: getWebsocketUrl },
        EventSocketService
    ]
})
export class AppModule {
}

export function getWebsocketUrl() {
    var port = '';
    var loc = window.location, new_uri;
    if (loc.protocol === "https:") {
        new_uri = "wss:";
    } else {
        new_uri = "ws:";
    }
    new_uri += "//" + loc.host;
    new_uri += "/api/events";
    return new_uri;
}

export function getBaseUrl() {
    return document.getElementsByTagName('base')[0].href;
}
