import { NgModule } from '@angular/core';
import { ServerModule } from '@angular/platform-server';
import { AppModuleShared } from './app.shared.module';
import { AppComponent } from './components/app/app.component';
import { Observable } from 'rxjs/Observable';
import { EventSocketService } from './services/eventsocket/eventsocket.service';

@NgModule({
    providers: [
        {provide: EventSocketService, useFactory: createMockSocketService }
    ],
    bootstrap: [ AppComponent ],
    imports: [
        ServerModule,
        AppModuleShared
    ]
})
export class AppModule {
}

export function createMockSocketService() {
    return {
        "messages": new Observable<any>(),
        SubscribeEvent: function() {

        }
    }
}