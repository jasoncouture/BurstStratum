import { Component, Inject, ChangeDetectorRef, OnInit } from '@angular/core';
import { Http } from '@angular/http';
import { EventSocketService } from '../../services/eventsocket/eventsocket.service';
import { OnDestroy } from '@angular/core/src/metadata/lifecycle_hooks';
import { ActivatedRoute } from '@angular/router';

@Component({
    selector: 'fetchdata',
    templateUrl: './fetchdata.component.html'
})
export class FetchDataComponent implements OnInit, OnDestroy {
    public messages: string[];

    constructor(http: Http, private eventSocket: EventSocketService, private ref: ChangeDetectorRef, private route: ActivatedRoute) {
        this.messages = [];
        this.eventSocket.messages.subscribe(message => {
            if (/^Share\.Account\.Accepted.\d+/.test(message.event)) {
                this.messages.push(message.data.account + 'Share accepted! Earned' + message.data.shares + ' shares.');
            } else if (message.event == 'Block.Update') {
                this.messages.push('New block: ' + message.data.height + ', base target: ' + message.data.baseTarget);
            } else if (message.event == 'Connected') {
                this.messages.push('Connected.');
            } else {
                return;
            }
            this.ref.detectChanges();
        });
        this.eventSocket.SubscribeEvent("Share.Account.Accepted");
        this.eventSocket.SubscribeEvent("Block.Update");
    }
    private sub: any;
    private id: any;
    ngOnInit() {
        this.sub = this.route.params.subscribe(params => {
            if (this.id) {
                this.eventSocket.UnsubscribeEvent('Share.Account.Accepted.' + this.id);
            } else {
                this.eventSocket.UnsubscribeEvent('Share.Account.Accepted');
            }
            if (params['id']) {
                this.id = params['id'];
                this.eventSocket.SubscribeEvent('Share.Account.Accepted.' + this.id);
            } else {
                this.id = null;
                this.eventSocket.SubscribeEvent('Share.Account.Accepted');
            }

        });
    }

    ngOnDestroy() {

    }
}

