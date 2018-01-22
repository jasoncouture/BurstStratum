import { Subject } from 'rxjs/Subject';
import { Subscription } from 'rxjs/Subscription';
import { Observable } from 'rxjs/Observable';
import { Observer } from 'rxjs/Observer';
import { Injectable, Inject } from '@angular/core';
import websocketConnect, { IWebSocket, Connection } from 'rxjs-websockets'
import { QueueingSubject } from 'queueing-subject'
import 'rxjs/add/operator/map';
import 'rxjs/add/operator/filter';
import 'rxjs/add/operator/retry';
import 'rxjs/add/operator/delay';
import 'rxjs/add/operator/retrywhen';

Injectable()
export class EventSocketService {
    private input: Subject<string>;
    private events: string[];
    private websocket: IWebSocket;
    private url: string;
    private connection: Connection;
    private subject: Subject<any>;
    public messages: Observable<any>;
    constructor( @Inject('WEBSOCKET_URL') websocketUrl: string) {
        this.events = [];
        this.input = new QueueingSubject<string>();
        this.subject = new Subject<any>();
        this.url = websocketUrl;
        this.messages = websocketConnect(
            websocketUrl,
            this.input,
            undefined,
            (url, protocols) => {
                this.websocket = new WebSocket(url, protocols);
                this.websocket.onopen = () => {
                    if (this.events.length > 0)
                        this.input.next(JSON.stringify({
                            "subscribe": this.events
                        }));
                }
                return this.websocket;
            }
        ).messages.retryWhen(errors => errors.delay(1000)).map(message => JSON.parse(message));
    }
    public UnsubscribeEvent(event: string): void {
        this.events = this.events.filter(s => s != event);
        this.input.next(JSON.stringify({
            "unsubscribe": event
        }));
    }
    public SubscribeEvent(event: string): void {
        this.events.push(event);
        this.input.next(JSON.stringify({
            "subscribe": event
        }));
    }
}