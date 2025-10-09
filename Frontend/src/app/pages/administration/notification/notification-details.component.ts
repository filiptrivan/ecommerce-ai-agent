import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Notification } from 'src/app/business/entities/business-entities.generated';
import { ApiService } from 'src/app/business/services/api/api.service';
import { NotificationBaseDetailsComponent } from 'src/app/business/components/base-details/business-base-details.generated';
import { BaseFormCopy, SpiderlyFormGroup, SpiderlyFormControl, SpiderlyButton, SpiderlyMessageService, BaseFormService, IsAuthorizedForSaveEvent, SpiderlyPanelsModule, SpiderlyControlsModule } from 'spiderly';

@Component({
    selector: 'notification-details',
    templateUrl: './notification-details.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyPanelsModule,
        SpiderlyControlsModule,
        NotificationBaseDetailsComponent
    ]
})
export class NotificationDetailsComponent extends BaseFormCopy implements OnInit {
    notificationFormGroup = new SpiderlyFormGroup<Notification>({});

    isMarkedAsRead = new SpiderlyFormControl<boolean>(true, {updateOn: 'change'});

    additionalButtons: SpiderlyButton[] = [];
    sendEmailNotificationButton = new SpiderlyButton({label: this.translocoService.translate('SendEmailNotification'), icon: 'pi pi-send', disabled: true});

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService, 
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router, 
        protected override route: ActivatedRoute,
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService,
        private apiService: ApiService,
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }

    override ngOnInit() {
        this.sendEmailNotificationButton.onClick = this.sendEmailNotification;
    }

    isAuthorizedForSaveChange = (event: IsAuthorizedForSaveEvent) => {
        this.sendEmailNotificationButton.disabled = !event.isAuthorizedForSave;

        if (event.isAuthorizedForSave) {
            this.isMarkedAsRead.enable();
        }
        else{
            this.isMarkedAsRead.disable();
        }
    }

    onAfterFormGroupInit() {
        if (this.notificationFormGroup.controls.id.value > 0) {
            this.additionalButtons.push(this.sendEmailNotificationButton);
        }
    }

    // We must to do it like arrow function
    sendEmailNotification = () => {
        this.apiService.sendNotificationEmail(this.notificationFormGroup.controls.id.value, this.notificationFormGroup.controls.version.value).subscribe(() => {
            this.messageService.successMessage(this.translocoService.translate('SuccessfulAttempt'));
        });
    }

    override onBeforeSave = (): void => {
        this.saveBody.isMarkedAsRead = this.isMarkedAsRead.value;
    }

}

