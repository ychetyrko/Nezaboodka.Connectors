CREATE TABLE `${db_name}`.`referencial_index` (
	`index_id` INT PRIMARY KEY NOT NULL,
	`field_name` VARCHAR(255) NOT NULL,
	`field_id` INT NOT NULL,

	FOREIGN KEY (`index_id`)
		REFERENCES `index_base`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;
