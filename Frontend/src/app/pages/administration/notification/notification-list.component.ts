import { Component, OnInit } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Column, SpiderlyDataTableComponent } from 'spiderly';
import { ApiService } from 'src/app/business/services/api/api.service';
import { Notification } from 'src/app/business/entities/business-entities.generated';

@Component({
    selector: 'notification-list',
    templateUrl: './notification-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent
    ]
})
export class NotificationListComponent implements OnInit {
    cols: Column<Notification>[];

    getPaginatedNotificationListObservableMethod = this.apiService.getPaginatedNotificationList;
    exportNotificationListToExcelObservableMethod = this.apiService.exportNotificationListToExcel;
    deleteNotificationObservableMethod = this.apiService.deleteNotification;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.cols = [
            {name: this.translocoService.translate('Title'), filterType: 'text', field: 'title'},
            {name: this.translocoService.translate('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true},
            {actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name: this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
        ]
    }

}

